using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Client interface for Suricata IDS/IPS integration.
/// Parses EVE JSON logs to extract security alerts.
/// </summary>
public interface ISuricataClient : IServiceClient
{
    /// <summary>
    /// Gets security alerts from Suricata logs.
    /// </summary>
    /// <param name="severity">Filter by severity (critical, high, medium, low). Null for all.</param>
    /// <param name="limit">Maximum number of alerts to return (default: 50)</param>
    /// <returns>List of security alerts</returns>
    Task<List<SecurityAlert>> GetAlertsAsync(string? severity = null, int limit = 50);

    /// <summary>
    /// Gets statistics about detected threats and alerts.
    /// </summary>
    /// <returns>Dictionary with alert counts by severity, category, etc.</returns>
    Task<Dictionary<string, object>> GetStatsAsync();
}
