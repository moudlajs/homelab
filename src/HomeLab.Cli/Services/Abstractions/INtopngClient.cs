using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Client interface for ntopng network traffic monitoring.
/// </summary>
public interface INtopngClient : IServiceClient
{
    /// <summary>
    /// Gets all tracked devices from ntopng.
    /// </summary>
    /// <returns>List of devices with traffic statistics</returns>
    Task<List<DeviceTraffic>> GetDevicesAsync();

    /// <summary>
    /// Gets overall network traffic statistics.
    /// </summary>
    /// <param name="deviceIp">Optional: Get stats for specific device only</param>
    /// <returns>Traffic statistics</returns>
    Task<TrafficStats> GetTrafficStatsAsync(string? deviceIp = null);
}
