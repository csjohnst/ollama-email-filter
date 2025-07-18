# AI Provider Configuration Guide

This email assistant supports multiple AI providers. You can switch between them by updating the configuration.

## Prompt Template Configuration

The application now supports configurable prompt templates, allowing you to customize the AI instructions without recompiling the application.

### Prompt Template Placeholders

The `PromptTemplate` setting supports the following placeholders:

- `{PromptRatings}` - Replaced with the content from `PromptRatings` setting
- `{EmailJson}` - Replaced with the JSON representation of the email being processed

### Default Prompt Template

```
You are to rate this email based on the following criteria:

{PromptRatings}

All other topics should be rated somewhere between 1 and 7 based on how important you think they are

Keep your output in json format, do not provide any other information or tell me why you rated it that way, just provide the rating.

Here is an example:
{
  "Subject" : "Bills are due",
  "From" : "John",
  "Date" : "2021-09-01",
  "Rating" : 10
}

input:
{EmailJson}
```

### Customizing the Prompt

You can customize the prompt by modifying the `PromptTemplate` setting in your configuration:

```json
{
  "AISettings": {
    "PromptTemplate": "Rate this email on a scale of 1-10:\n\n{PromptRatings}\n\nEmail: {EmailJson}\n\nResponse format: {\"rating\": number}"
  }
}
```

## Supported Providers

### 1. Ollama (Local AI)

**Configuration:**

```json
{
  "AISettings": {
    "Provider": "ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "ModelName": "llama3"
    }
  }
}
```

**Setup:**

1. Install Ollama from https://ollama.ai/
2. Download a model: `ollama pull llama3`
3. Start Ollama service: `ollama serve`
4. Set the BaseUrl to your Ollama server address

### 2. OpenAI

**Configuration:**

```json
{
  "AISettings": {
    "Provider": "openai",
    "OpenAI": {
      "ApiKey": "sk-your-openai-api-key-here",
      "Model": "gpt-3.5-turbo"
    }
  }
}
```

**Setup:**

1. Get an API key from https://platform.openai.com/api-keys
2. Available models: gpt-3.5-turbo, gpt-4, gpt-4-turbo
3. Add your API key to the configuration

### 3. Azure OpenAI

**Configuration:**

```json
{
  "AISettings": {
    "Provider": "azureopenai",
    "AzureOpenAI": {
      "ApiKey": "your-azure-openai-api-key",
      "Endpoint": "https://your-resource.openai.azure.com",
      "DeploymentName": "your-deployment-name",
      "ApiVersion": "2024-02-15-preview"
    }
  }
}
```

**Setup:**

1. Create an Azure OpenAI resource in Azure Portal
2. Deploy a model (e.g., gpt-35-turbo or gpt-4)
3. Get the endpoint, API key, and deployment name from Azure Portal
4. Update the configuration with your Azure details

### 4. Anthropic (Claude)

**Configuration:**

```json
{
  "AISettings": {
    "Provider": "anthropic",
    "Anthropic": {
      "ApiKey": "sk-ant-your-anthropic-api-key",
      "Model": "claude-3-haiku-20240307"
    }
  }
}
```

**Setup:**

1. Get an API key from https://console.anthropic.com/
2. Available models: claude-3-haiku-20240307, claude-3-sonnet-20240229, claude-3-opus-20240229
3. Add your API key to the configuration

## Configuration File Management

### Production Configuration (`appsettings.json`)

Keep this file with placeholder values and commit it to source control:

```json
{
  "AISettings": {
    "Provider": "ollama",
    "OpenAI": {
      "ApiKey": "your-openai-api-key",
      "Model": "gpt-3.5-turbo"
    }
  }
}
```

### Local Configuration (`appsettings.local.json`)

Create this file with your actual API keys (this file is git-ignored):

```json
{
  "AISettings": {
    "Provider": "openai",
    "OpenAI": {
      "ApiKey": "sk-proj-actual-api-key-here",
      "Model": "gpt-4"
    }
  }
}
```

## Switching Providers

To switch AI providers, simply change the `Provider` value in your configuration:

```json
{
  "AISettings": {
    "Provider": "openai" // Change this to: "ollama", "openai", "azureopenai", or "anthropic"
  }
}
```

## Cost Considerations

- **Ollama**: Free (runs locally, requires local resources)
- **OpenAI**: Pay per token (check pricing at https://openai.com/pricing)
- **Azure OpenAI**: Pay per token (Azure pricing)
- **Anthropic**: Pay per token (check pricing at https://www.anthropic.com/pricing)

## Error Handling

The application will show helpful error messages if:

- API keys are missing or invalid
- The selected provider is not supported
- Network connectivity issues occur
- Rate limits are exceeded

## Performance Notes

- **Ollama**: Slowest, but free and private
- **OpenAI/Azure**: Fast, reliable, costs money
- **Anthropic**: Fast, good reasoning, costs money

Choose the provider that best fits your needs for cost, speed, and privacy.
