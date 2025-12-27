using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for Prometheus monitoring operations.
/// </summary>
public interface IPrometheusClient : IServiceClient
{
    /// <summary>
    /// Gets active alerts.
    /// </summary>
    Task<List<AlertInfo>> GetActiveAlertsAsync();

    /// <summary>
    /// Gets scrape targets status.
    /// </summary>
    Task<List<TargetInfo>> GetTargetsAsync();

    /// <summary>
    /// Executes a PromQL query.
    /// </summary>
    Task<string> QueryAsync(string query);
}
