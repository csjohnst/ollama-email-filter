using OllamaSharp;
using System.Text;

namespace OllamaEmailFilter
{
    public class OllamaAIService : IAIService
    {
        private readonly OllamaApiClient _client;
        private readonly string _modelName;

        public OllamaAIService(string baseUrl, string modelName)
        {
            _client = new OllamaApiClient(new Uri(baseUrl));
            _client.SelectedModel = modelName;
            _modelName = modelName;
        }

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var request = new OllamaSharp.Models.GenerateCompletionRequest
            {
                Model = _modelName,
                Prompt = prompt
            };

            var responseBuilder = new StringBuilder();
            var streamer = new StringBuilderResponseStreamer(responseBuilder);

            await _client.StreamCompletion(request, streamer, cancellationToken);
            return responseBuilder.ToString();
        }

        private class StringBuilderResponseStreamer : OllamaSharp.Streamer.IResponseStreamer<OllamaSharp.Models.GenerateCompletionResponseStream>
        {
            private readonly StringBuilder _builder;
            public StringBuilderResponseStreamer(StringBuilder builder) => _builder = builder;

            public Task OnResponseAsync(OllamaSharp.Models.GenerateCompletionResponseStream response, CancellationToken cancellationToken = default)
            {
                _builder.Append(response.Response);
                return Task.CompletedTask;
            }

            public void Stream(OllamaSharp.Models.GenerateCompletionResponseStream response)
            {
                _builder.Append(response.Response);
            }
        }
    }
}
