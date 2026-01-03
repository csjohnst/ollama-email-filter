namespace OllamaEmailFilter.Configuration;

public class ServiceSettings
{
    public int PollingIntervalMinutes { get; set; } = 5;
    public int HealthCheckPort { get; set; } = 8080;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
}
