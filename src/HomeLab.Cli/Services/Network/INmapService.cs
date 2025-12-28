using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Network;

/// <summary>
/// Service interface for network scanning using nmap.
/// </summary>
public interface INmapService
{
    /// <summary>
    /// Scans a network range for active devices.
    /// </summary>
    /// <param name="networkRange">CIDR network range (e.g., "192.168.1.0/24")</param>
    /// <param name="quickScan">If true, performs quick scan without port detection</param>
    /// <returns>List of discovered network devices</returns>
    Task<List<NetworkDevice>> ScanNetworkAsync(string networkRange, bool quickScan = false);

    /// <summary>
    /// Scans ports on a specific device.
    /// </summary>
    /// <param name="ipAddress">IP address of the device to scan</param>
    /// <param name="commonPortsOnly">If true, scans only common ports (faster)</param>
    /// <returns>List of port scan results</returns>
    Task<List<PortScanResult>> ScanPortsAsync(string ipAddress, bool commonPortsOnly = true);

    /// <summary>
    /// Checks if nmap is installed and available.
    /// </summary>
    /// <returns>True if nmap is available, false otherwise</returns>
    bool IsNmapAvailable();
}
