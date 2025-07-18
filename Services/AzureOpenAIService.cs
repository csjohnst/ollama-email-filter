using System.Text;
using System.Text.Json;

namespace OllamaEmailFilter
{
    public class AzureOpenAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _deploymentName;
        private readonly string _endpoint;
        private readonly string _apiVersion;

        public AzureOpenAIService(string apiKey, string endpoint, string deploymentName, string apiVersion = "2024-02-15-preview")
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _deploymentName = deploymentName;
            _endpoint = endpoint.TrimEnd('/');
            _apiVersion = apiVersion;
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1000,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseJson);

            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
