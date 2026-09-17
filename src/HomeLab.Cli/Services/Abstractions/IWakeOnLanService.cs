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

    /// <summary>
    /// Checks if a TCP port is accepting connections on the given host.
    /// Unlike <see cref="IsReachableAsync"/>, this identifies a specific service
    /// rather than merely whoever currently holds the address, so it does not
    /// report success when an unrelated device has taken over a stale IP.
    /// </summary>
    /// <param name="ipAddress">IP address to probe</param>
    /// <param name="port">TCP port to probe</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>True if the port accepted a connection</returns>
    Task<bool> IsPortOpenAsync(string ipAddress, int port, int timeoutMs = 3000);
}
