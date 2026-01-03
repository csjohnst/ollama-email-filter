namespace OllamaEmailFilter.Services;

public interface IEmailProcessingService
{
    Task ProcessEmailsAsync(CancellationToken cancellationToken = default);
}
