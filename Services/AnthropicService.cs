using System.Text;
using System.Text.Json;

namespace OllamaEmailFilter
{
    public class AnthropicService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;

        public AnthropicService(string apiKey, string model = "claude-3-haiku-20240307", string baseUrl = "https://api.anthropic.com/v1")
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _model = model;
            _baseUrl = baseUrl;
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = 1000,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/messages", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseJson);

            return document.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
