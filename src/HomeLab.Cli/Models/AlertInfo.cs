namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a Prometheus alert.
/// </summary>
public class AlertInfo
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty; // firing, pending, inactive
    public string Severity { get; set; } = string.Empty; // critical, warning, info
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ActiveAt { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// Represents a Prometheus scrape target.
/// </summary>
public class TargetInfo
{
    public string Job { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty; // up, down, unknown
    public DateTime? LastScrape { get; set; }
    public double? ScrapeDuration { get; set; }
    public string? Error { get; set; }
}
