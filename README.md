# ollama-email-filter
C# console app for reading emails from IMAP folder and passing them through private AI (Ollama) to mark important or read

This is just a personal weekend project, created to scratch an itch. Do with it what you will.

You will need an Ollama server setup and capable of responding to API calls either from the local machine or allowing connection from remote sources.

This has been tested using the llama3 model with decent results, however other models may work just as well.

The project includes a sample appsettings.json file outlining the required settings for the IMAP server and Ollama server. There is no provision for other methods for connecting to email such as Gmail, M365, POP3 etc...

Use the PromptRatings app setting to specify the types of emails and ratings that are relevant to you.

Anything equal or greater than 7 will be flagged
Anything between 3 and 6 will be left unread
Anything under 3 will be marked as read

Currently this will only run on the main inbox folder.

This allows you to run on your inbox folder without any destructive behaviour, but highligh emails that a human should most probably read.