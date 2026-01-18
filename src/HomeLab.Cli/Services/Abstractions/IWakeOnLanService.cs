namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for Wake-on-LAN operations.
/// </summary>
public interface IWakeOnLanService
{
    /// <summary>
    /// Sends a Wake-on-LAN magic packet to wake a device.
    /// </summary>
    /// <param name="macAddress">MAC address in format XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX</param>
    /// <param name="broadcastAddress">Optional broadcast address (defaults to 255.255.255.255)</param>
    /// <param name="port">Optional port (defaults to 9)</param>
    /// <returns>True if packet was sent successfully</returns>
    Task<bool> WakeAsync(string macAddress, string? broadcastAddress = null, int port = 9);

    /// <summary>
    /// Checks if a device is reachable (ping).
    /// </summary>
    /// <param name="ipAddress">IP address to ping</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>True if device responds to ping</returns>
    Task<bool> IsReachableAsync(string ipAddress, int timeoutMs = 3000);
}
