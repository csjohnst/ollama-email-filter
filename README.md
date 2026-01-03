# Email Assistant

A .NET 8.0 application that uses AI to intelligently filter and organize emails via IMAP. Runs as a continuously polling Docker service.

This is just a personal weekend project, created to scratch an itch. Do with it what you will.

## Features

- Continuously polls your IMAP inbox for new emails
- Uses AI to rate email importance (0-10 scale)
- **Optional email categorization** into Inbox subfolders (Bills, Shopping, Work, etc.)
- Automatically organizes emails based on rating:
  - **Rating 7+**: Flagged as important
  - **Rating 4-6**: Left unread for review
  - **Rating 1-3**: Moved to Archived folder
  - **Rating 0**: Moved to Junk folder
- Health check endpoint for container orchestration
- Supports multiple AI providers

## AI Provider Support

- **Ollama** - For local AI models (default)
- **OpenAI** - Using OpenAI's API
- **Azure OpenAI** - Using Azure's OpenAI service
- **Anthropic** - Using Claude models

## Quick Start with Docker

### 1. Create environment file

```bash
cp .env.example .env
```

Edit `.env` with your email credentials and AI provider settings:

```bash
# Required
EMAIL_USERNAME=your-email@example.com
EMAIL_PASSWORD=your-app-password
EMAIL_HOST=imap.example.com

# AI Provider (ollama, openai, azureopenai, anthropic)
AI_PROVIDER=ollama
```

### 2. Run with Docker Compose

**With local Ollama:**
```bash
docker-compose -f docker-compose.yml -f docker-compose.ollama.yml up -d
```

**With OpenAI:**
```bash
export OPENAI_API_KEY=sk-your-key
docker-compose -f docker-compose.yml -f docker-compose.openai.yml up -d
```

**With Azure OpenAI:**
```bash
export AZURE_OPENAI_API_KEY=your-key
export AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
export AZURE_OPENAI_DEPLOYMENT=your-deployment-name
docker-compose -f docker-compose.yml -f docker-compose.azureopenai.yml up -d
```

**With Anthropic:**
```bash
export ANTHROPIC_API_KEY=sk-ant-your-key
docker-compose -f docker-compose.yml -f docker-compose.anthropic.yml up -d
```

### 3. Check health

```bash
curl http://localhost:8080/health
```

## Environment Variables

All environment variables use the `EMAILASSISTANT_` prefix with double underscore (`__`) for nested settings.

### Required Settings

| Variable | Description |
|----------|-------------|
| `EMAIL_USERNAME` | Your email address |
| `EMAIL_PASSWORD` | Your email password or app password |
| `EMAIL_HOST` | IMAP server hostname |

### Optional Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `EMAIL_PORT` | 993 | IMAP port |
| `EMAIL_COUNT` | 100 | Max emails to process per cycle |
| `EMAIL_MAX_BODY_LENGTH` | 2000 | Max characters of email body to send to AI |
| `EMAIL_INCLUDE_READ` | false | Process already-read emails |
| `POLLING_INTERVAL_MINUTES` | 5 | Time between email checks |
| `AI_PROVIDER` | ollama | AI provider to use |

### Provider-Specific Settings

**Ollama:**
| Variable | Default |
|----------|---------|
| `OLLAMA_BASE_URL` | http://ollama:11434 |
| `OLLAMA_MODEL` | llama3 |

**OpenAI:**
| Variable | Default |
|----------|---------|
| `OPENAI_API_KEY` | (required) |
| `OPENAI_MODEL` | gpt-4.1-nano |

**Azure OpenAI:**
| Variable | Default |
|----------|---------|
| `AZURE_OPENAI_API_KEY` | (required) |
| `AZURE_OPENAI_ENDPOINT` | (required) |
| `AZURE_OPENAI_DEPLOYMENT` | (required) |
| `AZURE_OPENAI_API_VERSION` | 2024-02-15-preview |

**Anthropic:**
| Variable | Default |
|----------|---------|
| `ANTHROPIC_API_KEY` | (required) |
| `ANTHROPIC_MODEL` | claude-3-haiku-20240307 |

## Email Categorization (Optional)

When enabled, emails are automatically sorted into Inbox subfolders based on content. The AI determines both the importance rating AND the appropriate category.

### Enable Categories

```bash
ENABLE_CATEGORIES=true
```

### Available Categories

| Category | Default | Folder | Description |
|----------|---------|--------|-------------|
| Bills | Enabled | Inbox/Bills | Household bills, invoices, payment notices |
| Shopping | Enabled | Inbox/Shopping | Order confirmations, shipping, receipts |
| Work | Enabled | Inbox/Work | Work-related, job opportunities |
| Personal | Enabled | Inbox/Personal | Personal correspondence from family/friends |
| Education | Disabled | Inbox/Education | School notices, courses, learning |
| Social | Disabled | Inbox/Social | Social media notifications |
| Events | Disabled | Inbox/Events | Invitations, calendar events, RSVPs |
| Travel | Disabled | Inbox/Travel | Flights, hotels, itineraries |
| Finance | Disabled | Inbox/Finance | Banking, investments, statements |
| Health | Disabled | Inbox/Health | Medical appointments, prescriptions |

### Category Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ENABLE_CATEGORIES` | false | Master toggle for categorization |
| `CATEGORY_BILLS_ENABLED` | true | Enable Bills category |
| `CATEGORY_SHOPPING_ENABLED` | true | Enable Shopping category |
| `CATEGORY_WORK_ENABLED` | true | Enable Work category |
| `CATEGORY_PERSONAL_ENABLED` | true | Enable Personal category |
| `CATEGORY_EDUCATION_ENABLED` | false | Enable Education category |
| `CATEGORY_SOCIAL_ENABLED` | false | Enable Social category |
| `CATEGORY_EVENTS_ENABLED` | false | Enable Events category |
| `CATEGORY_TRAVEL_ENABLED` | false | Enable Travel category |
| `CATEGORY_FINANCE_ENABLED` | false | Enable Finance category |
| `CATEGORY_HEALTH_ENABLED` | false | Enable Health category |

### How It Works

1. Email is sent to AI with category-aware prompt
2. AI returns both Rating (0-10) and Category
3. If category matches an enabled category, email moves to `Inbox/{Category}`
4. Rating logic still applies (flagged if 7+, etc.)
5. If no category matches, falls back to rating-only behavior

## Health Check Endpoints

The service exposes health check endpoints on port 8080:

| Endpoint | Purpose |
|----------|---------|
| `/health` | Full health status with details |
| `/health/live` | Liveness probe (always returns 200 if running) |
| `/health/ready` | Readiness probe (503 if unhealthy) |

## Running Locally (without Docker)

### Prerequisites

- .NET 8.0 SDK
- One of: Ollama server, OpenAI API key, Azure OpenAI deployment, or Anthropic API key

### Build and Run

```bash
# Build
dotnet build

# Configure (create appsettings.local.json with your settings)
cp appsettings.json appsettings.local.json
# Edit appsettings.local.json with your credentials

# Run
dotnet run
```

## Configuration Files

- `appsettings.json` - Base configuration with defaults
- `appsettings.local.json` - Local overrides with secrets (git-ignored)
- `.env` - Docker environment variables (git-ignored)
- `.env.example` - Example environment variables

## Customizing Email Rating

Use the `PromptRatings` setting in `appsettings.json` to specify the types of emails and ratings relevant to you.

The `PromptTemplate` setting allows you to customize the AI instructions. The template supports placeholders:
- `{PromptRatings}` - Your rating criteria
- `{EmailJson}` - The email content being analyzed

## Project Structure

```
├── Program.cs                    # Application entry point with DI setup
├── Configuration/                # Strongly-typed settings classes
├── Services/
│   ├── AIServiceFactory.cs       # Creates AI provider instances
│   ├── EmailProcessingService.cs # Core email processing logic
│   └── *AIService.cs             # AI provider implementations
├── Workers/
│   └── EmailPollingWorker.cs     # Background service for polling
├── HealthChecks/
│   └── EmailServiceHealthCheck.cs
├── Dockerfile                    # Multi-stage Docker build
├── docker-compose.yml            # Main orchestration
└── docker-compose.*.yml          # Provider-specific configs
```

## License

Do with it what you will.
