using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaEmailFilter;
using OllamaEmailFilter.Configuration;
using OllamaEmailFilter.HealthChecks;
using OllamaEmailFilter.Services;
using OllamaEmailFilter.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuration sources (environment variables override JSON files)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "EMAILASSISTANT_");

// Bind strongly-typed configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<AISettings>(builder.Configuration.GetSection("AISettings"));
builder.Services.Configure<ServiceSettings>(builder.Configuration.GetSection("ServiceSettings"));

// Register AI service as singleton
builder.Services.AddSingleton<IAIService>(sp =>
{
    return AIServiceFactory.CreateAIService(builder.Configuration);
});

// Register email processing service as scoped (fresh IMAP connection per cycle)
builder.Services.AddScoped<IEmailProcessingService, EmailProcessingService>();

// Register background worker
builder.Services.AddHostedService<EmailPollingWorker>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<EmailServiceHealthCheck>("email_service");

// Configure Kestrel to listen on the health check port
var serviceSettings = builder.Configuration.GetSection("ServiceSettings").Get<ServiceSettings>() ?? new ServiceSettings();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(serviceSettings.HealthCheckPort);
});

var app = builder.Build();

// Validate required configuration on startup
ValidateConfiguration(app.Services);

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health/ready", async (HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    return report.Status == HealthStatus.Healthy
        ? Results.Ok(new { status = "Ready" })
        : Results.Json(new { status = report.Status.ToString() }, statusCode: 503);
});

app.Logger.LogInformation("Email Assistant Service starting...");
app.Logger.LogInformation("AI Provider: {Provider}", builder.Configuration["AISettings:Provider"] ?? "ollama");
app.Logger.LogInformation("Health check endpoint: http://0.0.0.0:{Port}/health", serviceSettings.HealthCheckPort);

await app.RunAsync();

static void ValidateConfiguration(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var emailSettings = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSettings>>().Value;

    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(emailSettings.Username))
        errors.Add("EmailSettings:Username is required");
    if (string.IsNullOrWhiteSpace(emailSettings.Password))
        errors.Add("EmailSettings:Password is required");
    if (string.IsNullOrWhiteSpace(emailSettings.Host))
        errors.Add("EmailSettings:Host is required");

    if (errors.Count > 0)
    {
        foreach (var error in errors)
        {
            logger.LogError("Configuration error: {Error}", error);
        }
        throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", errors)}");
    }

    logger.LogInformation("Configuration validated successfully");
    logger.LogInformation("Email Host: {Host}", emailSettings.Host);
    logger.LogInformation("Email Count: {Count}", emailSettings.EmailCount);
}
