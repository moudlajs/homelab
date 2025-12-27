namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a Grafana dashboard.
/// </summary>
public class DashboardInfo
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsStarred { get; set; }
}
