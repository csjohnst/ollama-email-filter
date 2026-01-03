namespace OllamaEmailFilter.Configuration;

public class EmailSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public int EmailCount { get; set; } = 10;
    public int MaxBodyLength { get; set; } = 2000;
    public bool IncludeReadEmails { get; set; } = false;
}
