using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for Grafana dashboard operations.
/// </summary>
public interface IGrafanaClient : IServiceClient
{
    /// <summary>
    /// Gets a list of available dashboards.
    /// </summary>
    Task<List<DashboardInfo>> GetDashboardsAsync();

    /// <summary>
    /// Opens a dashboard in the browser.
    /// </summary>
    Task OpenDashboardAsync(string uid);

    /// <summary>
    /// Gets the Grafana URL for direct access.
    /// </summary>
    string GetDashboardUrl(string uid = "");
}
