using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using OllamaEmailFilter.Configuration;
using OllamaEmailFilter.HealthChecks;
using OllamaEmailFilter.Services;

namespace OllamaEmailFilter.Workers;

public class EmailPollingWorker : BackgroundService
{
    private readonly ILogger<EmailPollingWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceSettings _settings;
    private int _consecutiveFailures = 0;

    public EmailPollingWorker(
        ILogger<EmailPollingWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<ServiceSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Polling Worker starting. Polling every {Interval} minutes",
            _settings.PollingIntervalMinutes);

        // Initial delay to allow health check to report healthy before first run
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting email processing cycle at {Time}", DateTimeOffset.Now);
                EmailServiceHealthCheck.SetProcessing(true);

                // Create a new scope for each processing cycle to get fresh services
                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

                await emailService.ProcessEmailsAsync(stoppingToken);

                _consecutiveFailures = 0;
                EmailServiceHealthCheck.ReportSuccess();
                _logger.LogInformation("Email processing cycle completed successfully");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Email Polling Worker is stopping");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Error during email processing (attempt {Attempts}/{MaxAttempts})",
                    _consecutiveFailures, _settings.MaxRetryAttempts);

                if (_consecutiveFailures >= _settings.MaxRetryAttempts)
                {
                    _logger.LogWarning("Max retry attempts reached. Waiting for next regular interval.");
                    _consecutiveFailures = 0;
                }
                else
                {
                    // Short delay before retry
                    await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds), stoppingToken);
                    continue; // Skip the regular interval wait
                }
            }
            finally
            {
                EmailServiceHealthCheck.SetProcessing(false);
            }

            // Wait for the configured polling interval
            await Task.Delay(TimeSpan.FromMinutes(_settings.PollingIntervalMinutes), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email Polling Worker stopping gracefully");
        await base.StopAsync(cancellationToken);
    }
}
