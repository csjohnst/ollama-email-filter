# email-assistant

C# console app for reading emails from IMAP folder and passing them through AI (Ollama, OpenAI, Azure OpenAI, or Anthropic) to mark important or read

This is just a personal weekend project, created to scratch an itch. Do with it what you will.

## AI Provider Support

This application supports multiple AI providers:

- **Ollama** - For local AI models (default)
- **OpenAI** - Using OpenAI's API
- **Azure OpenAI** - Using Azure's OpenAI service
- **Anthropic** - Using Claude models

Configure your preferred provider in the `AISettings:Provider` setting.

## Setup

You will need one of the following AI services configured:

- **Ollama server** setup and capable of responding to API calls either from the local machine or allowing connection from remote sources
- **OpenAI API key** from https://platform.openai.com/
- **Azure OpenAI deployment** with endpoint and API key
- **Anthropic API key** from https://console.anthropic.com/

This has been tested using the llama3 model on Ollama and GPT-3.5-turbo on OpenAI with decent results, however other models may work just as well.

The project includes a sample appsettings.json file outlining the required settings for the IMAP server and AI providers. There is no provision for other methods for connecting to email such as Gmail, M365, POP3 etc...

Use the PromptRatings app setting to specify the types of emails and ratings that are relevant to you.

The PromptTemplate setting allows you to customize the AI instructions without recompiling the application. The template supports placeholders like {PromptRatings} and {EmailJson} for dynamic content insertion.

Anything equal or greater than 7 will be flagged
Anything between 3 and 6 will be left unread
Anything under 3 will be marked as read

Currently this will only run on the main inbox folder.

This allows you to run on your inbox folder without any destructive behaviour, but highlight emails that a human should most probably read.

## Configuration

Set the `AISettings:Provider` to one of: `ollama`, `openai`, `azureopenai`, or `anthropic`

Each provider has its own configuration section in appsettings.json with the required API keys and endpoints.
