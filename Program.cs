using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using OllamaSharp;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
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
        var port = int.Parse(configuration["EmailSettings:Port"]);
        var imapClient = new ImapClient();
        var uri = new Uri(configuration["OllamaSettings:Uri"]);
        var modelName = configuration["OllamaSettings:ModelName"];
        var promptRatings = configuration["OllamaSettings:PromptRatings"];
        var emailCount = int.Parse(configuration["EmailSettings:EmailCount"]);

        // set up the client

        var ollama = new OllamaApiClient(uri);

        // select a model which should be used for further operations
        ollama.SelectedModel = modelName;



        string prompt = @"

                            You are to rate this email based on the following criteria:";

        prompt += promptRatings;
        prompt += @"
                            
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
                            ";
        // Connect to the IMAP server


        using (var client = new ImapClient())
        {
            Console.WriteLine("Connecting to mail server {0}...", host);
            client.Connect(host, port, true);
            Console.WriteLine("Authenticating with server...");
            client.Authenticate(username, password);

            // The Inbox folder is always available on all IMAP servers...
            var inbox = client.Inbox;
            inbox.Open(FolderAccess.ReadWrite);
            inbox.OrderByDescending(x => x.Date);
            // Get the total number of messages in the inbox
            int messageCount = inbox.Count;

            // Determine the start index for the last 100 messages (or fewer if there are less than 100 messages)
            int startIndex = Math.Max(messageCount - emailCount, 0);
            Console.WriteLine("Fetching Messages from server...");
            // Fetch the latest 100 messages (or fewer)
            var messages = inbox.Fetch(startIndex, messageCount - 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId);
            Console.WriteLine("Filtering Messages to process only unread/unflagged messages...");
            // Filter the unread messages from the fetched messages
            var unreadMessages = messages.Where(m => !m.Flags.Value.HasFlag(MessageFlags.Seen) && !m.Flags.Value.HasFlag(MessageFlags.Flagged)).ToList();

            Console.WriteLine("Total messages: {0}", inbox.Count);
            Console.WriteLine("Unread Messages to Process: {0}", unreadMessages.Count);
            int count = 1;
            foreach (var message in unreadMessages)
            {
                Console.WriteLine("Processing message {0} of {1} - {2}", count++, unreadMessages.Count, message.Envelope.Subject);
                var email = new MyEmail();

                email.Subject = message.Envelope.Subject;

                email.From = message.Envelope.From.ToString();

                email.Date = message.Date.ToString();

                // IMessageSummary.TextBody is a convenience property that finds the 'text/plain' body part for us
                var bodyPart = message.TextBody;

                if (bodyPart != null)
                {
                    // check the client is still connected and if not reconnect
                    if (!client.IsConnected)
                    {
                        client.Connect(host, port, SecureSocketOptions.SslOnConnect);
                        client.Authenticate(username, password);
                    }
                    // download the 'text/plain' body part
                    var plain = (TextPart)client.Inbox.GetBodyPart(message.UniqueId, bodyPart);

                    // TextPart.Text is a convenience property that decodes the content and converts the result to
                    // a string for us
                    var text = plain.Text;

                    // truncate the text to 2000 characters to avoid exceeding the maximum input length for the model
                    if (text.Length > 2000)
                    {
                        text = text.Substring(0, 2000);
                    }
                    email.Message = text;
                }
                // Convert the list of emails to a JSON string using Newtonsoft.Json
                var json = JsonConvert.SerializeObject(email);

                StringBuilder responseBuilder = new StringBuilder();
                var context = await ollama.StreamCompletion(prompt + json, null, stream =>
                {
                    responseBuilder.Append(stream.Response);
                });

                string response = responseBuilder.ToString();
                try
                {
                    // Using dynamic for simplicity and to avoid creating a class for the response
                    dynamic emailRating = JsonConvert.DeserializeObject(response);
                    if (emailRating.Rating >= 7)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("This email is very important, mark it as flagged");
                        Console.ResetColor();
                        client.Inbox.AddFlags(message.UniqueId, MessageFlags.Flagged, true);
                    }
                    else if (emailRating.Rating >= 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("This email is maybe important, leave it unread");
                        Console.ResetColor();
                    }
                    else
                    {                        
                        Console.WriteLine("This email is not important, mark it as read");
                        client.Inbox.AddFlags(message.UniqueId, MessageFlags.Seen, true);   
                    }

                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: " + e.Message);
                    Console.ResetColor();
                }
            }

            client.Disconnect(true);
        }





    }
    public class EmailSettings
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public class AppSettings
    {
        public string Uri { get; set; }
        public string ModelName { get; set; }
        public string PromptRatings { get; set; }
    }

    private class MyEmail
    {
        public string Subject { get; set; }
        public string From { get; set; }
        public string Date { get; set; }
        public string Message { get; set; }
    }
}