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
    private readonly CategorySettings _categorySettings;
    private readonly IAIService _aiService;

    public EmailProcessingService(
        ILogger<EmailProcessingService> logger,
        IOptions<EmailSettings> emailSettings,
        IOptions<AISettings> aiSettings,
        IOptions<CategorySettings> categorySettings,
        IAIService aiService)
    {
        _logger = logger;
        _emailSettings = emailSettings.Value;
        _aiSettings = aiSettings.Value;
        _categorySettings = categorySettings.Value;
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
        string promptTemplate;

        // Use category template if categories are enabled
        if (_categorySettings.EnableCategories)
        {
            promptTemplate = _aiSettings.CategoryPromptTemplate;
            if (string.IsNullOrWhiteSpace(promptTemplate))
            {
                promptTemplate = @"
You are an email assistant. Your task is to rate the importance and categorize the following email.

Rating Criteria:
{PromptRatings}

Available Categories:
{Categories}

Instructions:
- Rate the email's importance from 0 (not important) to 10 (very important).
- Assign the email to ONE category from the list above, or use null if no category fits.
- Only output a single JSON object in the following format:

{
  ""Subject"": ""<subject>"",
  ""From"": ""<from>"",
  ""Date"": ""<date>"",
  ""Rating"": <number>,
  ""Category"": ""<category or null>""
}

Do not include any explanation, extra text, or formatting outside the JSON object.

Here is the email:
{EmailJson}";
            }

            // Build category list for prompt
            var enabledCategories = _categorySettings.Categories
                .Where(c => c.Value.Enabled)
                .Select(c => $"- {c.Key}: {c.Value.Description}")
                .ToList();

            var categoriesText = enabledCategories.Count > 0
                ? string.Join("\n", enabledCategories)
                : "No categories configured";

            promptTemplate = promptTemplate
                .Replace("{PromptRatings}", _aiSettings.PromptRatings ?? "")
                .Replace("{Categories}", categoriesText);
        }
        else
        {
            promptTemplate = _aiSettings.PromptTemplate;
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

            promptTemplate = promptTemplate.Replace("{PromptRatings}", _aiSettings.PromptRatings ?? "");
        }

        return promptTemplate;
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
        var categoryFolders = new Dictionary<string, IMailFolder?>();

        // Get or create Junk folder
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

        // Get or create Archived folder
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

        // Get or create Notifications folder
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

        // Get or create category folders as subfolders of Inbox
        if (_categorySettings.EnableCategories)
        {
            foreach (var category in _categorySettings.Categories.Where(c => c.Value.Enabled))
            {
                var folderName = string.IsNullOrWhiteSpace(category.Value.FolderName)
                    ? category.Key
                    : category.Value.FolderName;

                IMailFolder? categoryFolder = null;
                try
                {
                    // Try to get existing subfolder of inbox
                    categoryFolder = await inbox.GetSubfolderAsync(folderName, cancellationToken);
                }
                catch
                {
                    try
                    {
                        // Create as subfolder of inbox
                        categoryFolder = await inbox.CreateAsync(folderName, true, cancellationToken);
                        _logger.LogInformation("Created category folder: Inbox/{FolderName}", folderName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not create category folder {FolderName}: {Message}",
                            folderName, ex.Message);
                    }
                }

                categoryFolders[category.Key] = categoryFolder;
            }
        }

        return new EmailFolders(junkFolder, archivedFolder, notificationsFolder, categoryFolders);
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
            string? category = null;

            if (emailRating != null)
            {
                try
                {
                    var ratingProperty = emailRating.Rating;
                    if (ratingProperty != null)
                    {
                        rating = (int?)Convert.ToInt32(ratingProperty.ToString());
                    }

                    // Parse category if categories are enabled
                    if (_categorySettings.EnableCategories)
                    {
                        var categoryProperty = emailRating.Category;
                        if (categoryProperty != null)
                        {
                            var categoryValue = categoryProperty.ToString();
                            if (!string.IsNullOrWhiteSpace(categoryValue) && categoryValue.ToLower() != "null")
                            {
                                category = categoryValue;
                            }
                        }
                    }
                }
                catch { }
            }

            if (rating.HasValue)
            {
                await ApplyRatingActionAsync(inbox, messageSummary, folders, rating.Value, category, cancellationToken);
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
        string? category,
        CancellationToken cancellationToken)
    {
        // Step 1: If categories enabled and category matched, move to category folder first
        if (_categorySettings.EnableCategories && !string.IsNullOrWhiteSpace(category))
        {
            if (folders.CategoryFolders.TryGetValue(category, out var categoryFolder) && categoryFolder != null)
            {
                try
                {
                    await inbox.MoveToAsync(messageSummary.UniqueId, categoryFolder, cancellationToken);
                    _logger.LogInformation("Rating {Rating}, Category {Category}: Moved to Inbox/{FolderName}",
                        rating, category, _categorySettings.GetFolderName(category));

                    // After moving, we need to work with the message in the new folder
                    // For rating actions (flagging), we need to re-fetch in the category folder
                    if (rating >= 7)
                    {
                        await categoryFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
                        // Find the message in the new folder (it will be the most recent)
                        var newSummaries = await categoryFolder.FetchAsync(
                            categoryFolder.Count - 1, categoryFolder.Count - 1,
                            MessageSummaryItems.UniqueId, cancellationToken);
                        var newSummary = newSummaries.FirstOrDefault();
                        if (newSummary != null)
                        {
                            await categoryFolder.AddFlagsAsync(newSummary.UniqueId, MessageFlags.Flagged, true, cancellationToken);
                            _logger.LogInformation("Marked as important (flagged)");
                        }
                        // Re-open inbox for further processing
                        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to move to category folder {Category}: {Message}. Falling back to rating-based action.",
                        category, ex.Message);
                }
            }
            else
            {
                _logger.LogDebug("Category {Category} not found in enabled folders, using rating-based action", category);
            }
        }

        // Step 2: Apply rating-based actions (if not moved to category or as fallback)
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

    private record EmailFolders(
        IMailFolder? Junk,
        IMailFolder? Archived,
        IMailFolder? Notifications,
        Dictionary<string, IMailFolder?> CategoryFolders);

    private class EmailData
    {
        public string? Subject { get; set; }
        public string? From { get; set; }
        public string? Date { get; set; }
        public string? Message { get; set; }
    }
}
