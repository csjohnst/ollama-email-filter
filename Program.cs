using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace OllamaEmailFilter
{
    class OllamaEmailFilter
    {
        static async Task Main(string[] args)
        {
            // Read the configuration file
            var configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                        .Build();

            var username = configuration["EmailSettings:Username"];
            var password = configuration["EmailSettings:Password"];
            var host = configuration["EmailSettings:Host"];
            var port = int.Parse(configuration["EmailSettings:Port"] ?? "993");
            var promptRatings = configuration["AISettings:PromptRatings"];
            var promptTemplate = configuration["AISettings:PromptTemplate"];
            var emailCount = int.Parse(configuration["EmailSettings:EmailCount"] ?? "10");
            var maxBodyLength = int.Parse(configuration["EmailSettings:MaxBodyLength"] ?? "2000");
            var includeReadEmails = bool.Parse(configuration["EmailSettings:IncludeReadEmails"] ?? "false");

            // Create AI service based on configuration
            IAIService aiService;
            try
            {
                aiService = AIServiceFactory.CreateAIService(configuration);
                Console.WriteLine($"AI Provider: {configuration["AISettings:Provider"]}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error initializing AI service: {ex.Message}");
                Console.ResetColor();
                return;
            }

            // Build the base prompt from configuration template
            string basePrompt = promptTemplate ?? @"
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

            // Replace the ratings placeholder
            basePrompt = basePrompt.Replace("{PromptRatings}", promptRatings ?? "");

            // Connect to the IMAP server


            using (var client = new ImapClient())
            {
                Console.WriteLine("Connecting to mail server {0}...", host);
                client.Connect(host, port, true);
                Console.WriteLine("Authenticating with server...");
                client.Authenticate(username, password);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadWrite);

                // Get or create the Junk, Archived, and Notifications folders
                var junkFolder = client.GetFolder("Junk") ?? client.GetFolder("Spam");
                if (junkFolder == null)
                {
                    // Try to create Junk folder if it doesn't exist
                    try
                    {
                        junkFolder = inbox.Create("Junk", true);
                    }
                    catch
                    {
                        Console.WriteLine("Warning: Could not find or create Junk folder. Items rated 0 will be marked as read instead.");
                    }
                }

                var archivedFolder = client.GetFolder("Archived");
                if (archivedFolder == null)
                {
                    // Try to create Archived folder if it doesn't exist
                    try
                    {
                        archivedFolder = inbox.Create("Archived", true);
                    }
                    catch
                    {
                        Console.WriteLine("Warning: Could not find or create Archived folder. Items rated 1-3 will be marked as read instead.");
                    }
                }

                var notificationsFolder = client.GetFolder("Notifications");
                if (notificationsFolder == null)
                {
                    // Try to create Notifications folder if it doesn't exist
                    try
                    {
                        notificationsFolder = inbox.Create("Notifications", true);
                    }
                    catch
                    {
                        Console.WriteLine("Warning: Could not find or create Notifications folder. Home automation alerts will be left in inbox.");
                    }
                }

                int messageCount = inbox.Count;
                int processed = 0;
                int count = 1;
                Console.WriteLine("Total messages: {0}", messageCount);
                Console.WriteLine("Processing up to {0} {1} messages...", emailCount, includeReadEmails ? "messages (including read)" : "unread/unflagged");

                // Fetch and process messages one by one, newest to oldest, up to emailCount
                for (int i = messageCount - 1; i >= 0 && processed < emailCount; i--)
                {
                    var messageSummary = inbox.Fetch(i, i, MessageSummaryItems.Full | MessageSummaryItems.UniqueId).FirstOrDefault();
                    if (messageSummary == null) continue;

                    // Skip already processed emails unless includeReadEmails is true
                    if (!includeReadEmails && messageSummary.Flags.HasValue && (messageSummary.Flags.Value.HasFlag(MessageFlags.Seen) || messageSummary.Flags.Value.HasFlag(MessageFlags.Flagged)))
                        continue;

                    Console.WriteLine("Processing message {0} of {1} - {2}", count++, emailCount, messageSummary.Envelope.Subject);

                    // Check for special case home automation notifications
                    string subject = messageSummary.Envelope.Subject ?? "";
                    bool isHomeAutomationNotification = subject.Contains("Animal Detected from Driveway Reolink") ||
                                                      subject.Contains("Person Detected from Driveway Reolink") ||
                                                      subject.Contains("Motion Detected from") ||
                                                      subject.Contains("Reolink");

                    if (isHomeAutomationNotification)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("Home automation notification detected, moving to Notifications folder");
                        Console.ResetColor();

                        if (notificationsFolder != null)
                        {
                            try
                            {
                                client.Inbox.MoveTo(messageSummary.UniqueId, notificationsFolder);
                                processed++;
                                continue; // Skip AI processing for these emails
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to move to Notifications folder: {ex.Message}. Processing with AI instead.");
                            }
                        }
                    }

                    var email = new MyEmail
                    {
                        Subject = messageSummary.Envelope.Subject,
                        From = messageSummary.Envelope.From.ToString(),
                        Date = messageSummary.Date.ToString()
                    };

                    var bodyPart = messageSummary.TextBody;
                    if (bodyPart != null)
                    {
                        var plain = client.Inbox.GetBodyPart(messageSummary.UniqueId, bodyPart) as TextPart;
                        if (plain != null)
                        {
                            var text = plain.Text;
                            if (text.Length > maxBodyLength)
                                text = text.Substring(0, maxBodyLength);
                            email.Message = text;
                        }
                    }
                    var json = JsonConvert.SerializeObject(email);

                    try
                    {
                        // Create the final prompt by replacing the email placeholder
                        string finalPrompt = basePrompt.Replace("{EmailJson}", json);
                        string response = await aiService.GenerateResponseAsync(finalPrompt);

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
                            if (rating.Value >= 7)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("This email is very important, mark it as flagged");
                                Console.ResetColor();
                                client.Inbox.AddFlags(messageSummary.UniqueId, MessageFlags.Flagged, true);
                            }
                            else if (rating.Value >= 4)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("This email is maybe important, leave it unread");
                                Console.ResetColor();
                            }
                            else if (rating.Value >= 1 && rating.Value <= 3)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine("This email is low importance, moving to Archived folder");
                                Console.ResetColor();
                                if (archivedFolder != null)
                                {
                                    try
                                    {
                                        client.Inbox.MoveTo(messageSummary.UniqueId, archivedFolder);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Failed to move to Archived folder: {ex.Message}. Marking as read instead.");
                                        client.Inbox.AddFlags(messageSummary.UniqueId, MessageFlags.Seen, true);
                                    }
                                }
                                else
                                {
                                    client.Inbox.AddFlags(messageSummary.UniqueId, MessageFlags.Seen, true);
                                }
                            }
                            else if (rating.Value == 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("This email is spam/junk, moving to Junk folder");
                                Console.ResetColor();
                                if (junkFolder != null)
                                {
                                    try
                                    {
                                        client.Inbox.MoveTo(messageSummary.UniqueId, junkFolder);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Failed to move to Junk folder: {ex.Message}. Marking as read instead.");
                                        client.Inbox.AddFlags(messageSummary.UniqueId, MessageFlags.Seen, true);
                                    }
                                }
                                else
                                {
                                    client.Inbox.AddFlags(messageSummary.UniqueId, MessageFlags.Seen, true);
                                }
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: Could not parse rating from model response.");
                            Console.ResetColor();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: " + e.Message);
                        Console.ResetColor();
                    }
                    processed++;
                }
                client.Disconnect(true);
            }





        }


        private class MyEmail
        {
            public string? Subject { get; set; }
            public string? From { get; set; }
            public string? Date { get; set; }
            public string? Message { get; set; }
        }
    }
}