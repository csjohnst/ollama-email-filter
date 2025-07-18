using Microsoft.Extensions.Configuration;

namespace OllamaEmailFilter
{
    public static class AIServiceFactory
    {
        public static IAIService CreateAIService(IConfigurationRoot configuration)
        {
            var provider = configuration["AISettings:Provider"]?.ToLowerInvariant();

            return provider switch
            {
                "ollama" => new OllamaAIService(
                    configuration["AISettings:Ollama:BaseUrl"] ?? "http://localhost:11434",
                    configuration["AISettings:Ollama:ModelName"] ?? "llama3"
                ),
                "openai" => new OpenAIService(
                    configuration["AISettings:OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is required"),
                    configuration["AISettings:OpenAI:Model"] ?? "gpt-3.5-turbo"
                ),
                "azureopenai" => new AzureOpenAIService(
                    configuration["AISettings:AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key is required"),
                    configuration["AISettings:AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint is required"),
                    configuration["AISettings:AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("Azure OpenAI deployment name is required"),
                    configuration["AISettings:AzureOpenAI:ApiVersion"] ?? "2024-02-15-preview"
                ),
                "anthropic" => new AnthropicService(
                    configuration["AISettings:Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic API key is required"),
                    configuration["AISettings:Anthropic:Model"] ?? "claude-3-haiku-20240307"
                ),
                _ => throw new InvalidOperationException($"Unsupported AI provider: {provider}. Supported providers: ollama, openai, azureopenai, anthropic")
            };
        }
    }
}
