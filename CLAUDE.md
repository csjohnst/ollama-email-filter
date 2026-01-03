# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project
dotnet build ollama-email-filter.csproj

# Run the application
dotnet run --project ollama-email-filter.csproj
```

There are no unit tests in this project.

## Architecture Overview

This is a .NET 8.0 console application that uses AI to intelligently filter and organize emails via IMAP.

### Core Components

**Program.cs** - Main entry point that:
- Loads configuration from `appsettings.json` (base) and `appsettings.local.json` (overrides)
- Connects to IMAP server and processes emails sequentially
- Sends email content to AI service for rating (0-10)
- Applies actions based on rating: move to Junk (0), Archive (1-3), leave unread (4-6), flag important (7+)

**IAIService.cs** - Interface defining the AI service contract with a single `GenerateResponseAsync` method.

**Services/** - AI provider implementations using Factory pattern:
- `AIServiceFactory.cs` - Creates appropriate service based on `AISettings:Provider` config value
- `OllamaAIService.cs` - Local Ollama models via OllamaSharp
- `OpenAIService.cs` - OpenAI API
- `AzureOpenAIService.cs` - Azure OpenAI deployments
- `AnthropicService.cs` - Anthropic Claude API

### Configuration

Uses Microsoft.Extensions.Configuration with two JSON files:
- `appsettings.json` - Base configuration (committed)
- `appsettings.local.json` - Local overrides with secrets (git-ignored)

Key configuration sections:
- `EmailSettings` - IMAP credentials, host, port, email count, body truncation length
- `AISettings` - Provider selection, prompt template with `{PromptRatings}` and `{EmailJson}` placeholders, provider-specific settings

### Email Processing Flow

1. Connect to IMAP server (port 993, SSL)
2. Create/get special folders: Junk, Archived, Notifications
3. Iterate through emails (newest first, skip read unless `IncludeReadEmails` is true)
4. Special handling for home automation notifications (detected via subject string matching)
5. Serialize email to JSON, substitute into prompt template
6. Parse AI JSON response for rating, apply corresponding action

### Key Dependencies

- MailKit (4.6.0) - IMAP email client
- OllamaSharp (1.1.10) - Ollama AI client
- Newtonsoft.Json (13.0.3) - JSON handling
- Microsoft.Extensions.Configuration.Json (8.0.0) - Configuration
