using System.Threading.Tasks;

namespace OllamaEmailFilter
{
    public interface IAIService
    {
        Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
