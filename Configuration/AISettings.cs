namespace OllamaEmailFilter.Configuration;

public class AISettings
{
    public string Provider { get; set; } = "ollama";
    public string PromptTemplate { get; set; } = string.Empty;
    public string CategoryPromptTemplate { get; set; } = string.Empty;
    public string PromptRatings { get; set; } = string.Empty;
    public OllamaSettings Ollama { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();
    public AnthropicSettings Anthropic { get; set; } = new();
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ModelName { get; set; } = "llama3";
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-3.5-turbo";
}

public class AzureOpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-02-15-preview";
}

public class AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-haiku-20240307";
}
