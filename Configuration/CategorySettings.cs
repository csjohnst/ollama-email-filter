namespace OllamaEmailFilter.Configuration;

public class CategorySettings
{
    public bool EnableCategories { get; set; } = false;
    public Dictionary<string, CategoryConfig> Categories { get; set; } = new();

    public IEnumerable<string> GetEnabledCategoryNames()
    {
        return Categories
            .Where(c => c.Value.Enabled)
            .Select(c => c.Key);
    }

    public string? GetFolderName(string categoryName)
    {
        if (Categories.TryGetValue(categoryName, out var config) && config.Enabled)
        {
            return string.IsNullOrWhiteSpace(config.FolderName) ? categoryName : config.FolderName;
        }
        return null;
    }
}

public class CategoryConfig
{
    public bool Enabled { get; set; } = false;
    public string FolderName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
