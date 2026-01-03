using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OllamaEmailFilter.Configuration;

namespace OllamaEmailFilter.Services;

public class EmailProcessingService : IEmailProcessingService
{
    private readonly ILogger<EmailProcessingService> _logger;
    private readonly EmailSettings _emailSettings;
    private readonly AISettings _aiSettings;
    private readonly IAIService _aiService;

    public EmailProcessingService(
        ILogger<EmailProcessingService> logger,
        IOptions<EmailSettings> emailSettings,
        IOptions<AISettings> aiSettings,
        IAIService aiService)
    {
        _logger = logger;
        _emailSettings = emailSettings.Value;
        _aiSettings = aiSettings.Value;
        _aiService = aiService;
    }

    public async Task ProcessEmailsAsync(CancellationToken cancellationToken = default)
    {
        var basePrompt = BuildPrompt();

        using var client = new ImapClient();
        try
        {
            await ConnectAndAuthenticateAsync(client, cancellationToken);
            var folders = await GetOrCreateFoldersAsync(client, cancellationToken);
            await ProcessInboxAsync(client, folders, basePrompt, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private string BuildPrompt()
    {
        var promptTemplate = _aiSettings.PromptTemplate;
        if (string.IsNullOrWhiteSpace(promptTemplate))
        {
            promptTemplate = @"
You are to rate this email based on the following criteria:

{PromptRatings}

All other topics should be rated somewhere between 1 and 7 based on how important you think they are

Keep your output in json format, do not provide any other information or tell me why you rated it that way, just provide the rating.

Here is an example:
{
  ""Subject"" : ""Bills are due"",
  ""From"" : ""John"",
  ""Date"" : ""2021-09-01"",
  ""Rating"" : 10
}

input:
{EmailJson}";
        }

        return promptTemplate.Replace("{PromptRatings}", _aiSettings.PromptRatings ?? "");
    }

    private async Task ConnectAndAuthenticateAsync(ImapClient client, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to mail server {Host}...", _emailSettings.Host);
        await client.ConnectAsync(_emailSettings.Host, _emailSettings.Port, true, cancellationToken);

        _logger.LogInformation("Authenticating with server...");
        await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cancellationToken);
    }

    private async Task<EmailFolders> GetOrCreateFoldersAsync(ImapClient client, CancellationToken cancellationToken)
    {
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        IMailFolder? junkFolder = null;
        IMailFolder? archivedFolder = null;
        IMailFolder? notificationsFolder = null;

        try
        {
            junkFolder = await client.GetFolderAsync("Junk", cancellationToken);
        }
        catch
        {
            try
            {
                junkFolder = await client.GetFolderAsync("Spam", cancellationToken);
            }
            catch
            {
                try
                {
                    junkFolder = await inbox.CreateAsync("Junk", true, cancellationToken);
                }
                catch
                {
                    _logger.LogWarning("Could not find or create Junk folder. Items rated 0 will be marked as read instead.");
                }
            }
        }

        try
        {
            archivedFolder = await client.GetFolderAsync("Archived", cancellationToken);
        }
        catch
        {
            try
            {
                archivedFolder = await inbox.CreateAsync("Archived", true, cancellationToken);
            }
            catch
            {
                _logger.LogWarning("Could not find or create Archived folder. Items rated 1-3 will be marked as read instead.");
            }
        }

        try
        {
            notificationsFolder = await client.GetFolderAsync("Notifications", cancellationToken);
        }
        catch
        {
            try
            {
                notificationsFolder = await inbox.CreateAsync("Notifications", true, cancellationToken);
            }
            catch
            {
                _logger.LogWarning("Could not find or create Notifications folder. Home automation alerts will be left in inbox.");
            }
        }

        return new EmailFolders(junkFolder, archivedFolder, notificationsFolder);
    }

    private async Task ProcessInboxAsync(
        ImapClient client,
        EmailFolders folders,
        string basePrompt,
        CancellationToken cancellationToken)
    {
        var inbox = client.Inbox;
        var messageCount = inbox.Count;
        var processed = 0;
        var count = 1;

        _logger.LogInformation("Total messages: {Count}", messageCount);
        _logger.LogInformation("Processing up to {EmailCount} {Type} messages...",
            _emailSettings.EmailCount,
            _emailSettings.IncludeReadEmails ? "messages (including read)" : "unread/unflagged");

        for (var i = messageCount - 1; i >= 0 && processed < _emailSettings.EmailCount; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var summaries = await inbox.FetchAsync(i, i, MessageSummaryItems.Full | MessageSummaryItems.UniqueId, cancellationToken);
            var messageSummary = summaries.FirstOrDefault();
            if (messageSummary == null) continue;

            // Skip already processed emails unless includeReadEmails is true
            if (!_emailSettings.IncludeReadEmails && messageSummary.Flags.HasValue &&
                (messageSummary.Flags.Value.HasFlag(MessageFlags.Seen) || messageSummary.Flags.Value.HasFlag(MessageFlags.Flagged)))
                continue;

            _logger.LogInformation("Processing message {Count} of {Total} - {Subject}",
                count++, _emailSettings.EmailCount, messageSummary.Envelope.Subject);

            // Check for special case home automation notifications
            var subject = messageSummary.Envelope.Subject ?? "";
            var isHomeAutomationNotification = subject.Contains("Animal Detected from Driveway Reolink") ||
                                               subject.Contains("Person Detected from Driveway Reolink") ||
                                               subject.Contains("Motion Detected from") ||
                                               subject.Contains("Reolink");

            if (isHomeAutomationNotification)
            {
                _logger.LogInformation("Home automation notification detected, moving to Notifications folder");

                if (folders.Notifications != null)
                {
                    try
                    {
                        await inbox.MoveToAsync(messageSummary.UniqueId, folders.Notifications, cancellationToken);
                        processed++;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to move to Notifications folder: {Message}. Processing with AI instead.", ex.Message);
                    }
                }
            }

            await ProcessSingleEmailAsync(client, inbox, messageSummary, folders, basePrompt, cancellationToken);
            processed++;
        }
    }

    private async Task ProcessSingleEmailAsync(
        ImapClient client,
        IMailFolder inbox,
        IMessageSummary messageSummary,
        EmailFolders folders,
        string basePrompt,
        CancellationToken cancellationToken)
    {
        var email = new EmailData
        {
            Subject = messageSummary.Envelope.Subject,
            From = messageSummary.Envelope.From.ToString(),
            Date = messageSummary.Date.ToString()
        };

        var bodyPart = messageSummary.TextBody;
        if (bodyPart != null)
        {
            var plain = await inbox.GetBodyPartAsync(messageSummary.UniqueId, bodyPart, cancellationToken) as TextPart;
            if (plain != null)
            {
                var text = plain.Text;
                if (text.Length > _emailSettings.MaxBodyLength)
                    text = text.Substring(0, _emailSettings.MaxBodyLength);
                email.Message = text;
            }
        }

        var json = JsonConvert.SerializeObject(email);

        try
        {
            var finalPrompt = basePrompt.Replace("{EmailJson}", json);
            var response = await _aiService.GenerateResponseAsync(finalPrompt, cancellationToken);

            dynamic? emailRating = JsonConvert.DeserializeObject(response);
            int? rating = null;
            if (emailRating != null)
            {
                try
                {
                    var ratingProperty = emailRating.Rating;
                    if (ratingProperty != null)
                    {
                        rating = (int?)Convert.ToInt32(ratingProperty.ToString());
                    }
                }
                catch { }
            }

            if (rating.HasValue)
            {
                await ApplyRatingActionAsync(inbox, messageSummary, folders, rating.Value, cancellationToken);
            }
            else
            {
                _logger.LogError("Could not parse rating from model response");
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error processing email: {Message}", e.Message);
        }
    }

    private async Task ApplyRatingActionAsync(
        IMailFolder inbox,
        IMessageSummary messageSummary,
        EmailFolders folders,
        int rating,
        CancellationToken cancellationToken)
    {
        if (rating >= 7)
        {
            _logger.LogInformation("Rating {Rating}: This email is very important, marking as flagged", rating);
            await inbox.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Flagged, true, cancellationToken);
        }
        else if (rating >= 4)
        {
            _logger.LogInformation("Rating {Rating}: This email is maybe important, leaving unread", rating);
        }
        else if (rating >= 1 && rating <= 3)
        {
            _logger.LogInformation("Rating {Rating}: This email is low importance, moving to Archived folder", rating);
            if (folders.Archived != null)
            {
                try
                {
                    await inbox.MoveToAsync(messageSummary.UniqueId, folders.Archived, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to move to Archived folder: {Message}. Marking as read instead.", ex.Message);
                    await inbox.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Seen, true, cancellationToken);
                }
            }
            else
            {
                await inbox.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Seen, true, cancellationToken);
            }
        }
        else if (rating == 0)
        {
            _logger.LogInformation("Rating {Rating}: This email is spam/junk, moving to Junk folder", rating);
            if (folders.Junk != null)
            {
                try
                {
                    await inbox.MoveToAsync(messageSummary.UniqueId, folders.Junk, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to move to Junk folder: {Message}. Marking as read instead.", ex.Message);
                    await inbox.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Seen, true, cancellationToken);
                }
            }
            else
            {
                await inbox.AddFlagsAsync(messageSummary.UniqueId, MessageFlags.Seen, true, cancellationToken);
            }
        }
    }

    private record EmailFolders(IMailFolder? Junk, IMailFolder? Archived, IMailFolder? Notifications);

    private class EmailData
    {
        public string? Subject { get; set; }
        public string? From { get; set; }
        public string? Date { get; set; }
        public string? Message { get; set; }
    }
}
