using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OllamaEmailFilter.Configuration;

namespace OllamaEmailFilter.HealthChecks;

public class EmailServiceHealthCheck : IHealthCheck
{
    private readonly EmailSettings _emailSettings;
    private readonly AISettings _aiSettings;
    private static DateTime _lastSuccessfulRun = DateTime.MinValue;
    private static bool _isProcessing = false;

    public EmailServiceHealthCheck(
        IOptions<EmailSettings> emailSettings,
        IOptions<AISettings> aiSettings)
    {
        _emailSettings = emailSettings.Value;
        _aiSettings = aiSettings.Value;
    }

    public static void ReportSuccess() => _lastSuccessfulRun = DateTime.UtcNow;
    public static void SetProcessing(bool processing) => _isProcessing = processing;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            { "lastSuccessfulRun", _lastSuccessfulRun == DateTime.MinValue ? "never" : _lastSuccessfulRun.ToString("o") },
            { "isProcessing", _isProcessing },
            { "emailHost", _emailSettings.Host },
            { "aiProvider", _aiSettings.Provider }
        };

        // Consider unhealthy if no successful run in the last hour
        if (_lastSuccessfulRun == DateTime.MinValue)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "No successful processing runs yet",
                data: data));
        }

        var timeSinceLastRun = DateTime.UtcNow - _lastSuccessfulRun;
        if (timeSinceLastRun > TimeSpan.FromHours(1))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Last successful run was {timeSinceLastRun.TotalMinutes:F0} minutes ago",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Last successful run {timeSinceLastRun.TotalMinutes:F0} minutes ago",
            data: data));
    }
}
