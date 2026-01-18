using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for WireGuard VPN operations.
/// </summary>
public interface IWireGuardClient : IServiceClient
{
    /// <summary>
    /// Lists all VPN peers.
    /// </summary>
    Task<List<VpnPeer>> GetPeersAsync();

    /// <summary>
    /// Adds a new VPN peer.
    /// </summary>
    /// <param name="name">Peer name (e.g., "danny-phone")</param>
    /// <returns>Configuration file content for the peer</returns>
    Task<string> AddPeerAsync(string name);

    /// <summary>
    /// Removes a VPN peer.
    /// </summary>
    Task RemovePeerAsync(string name);

    /// <summary>
    /// Generates a QR code for a peer configuration.
    /// </summary>
    Task<byte[]> GenerateQRCodeAsync(string peerConfig);

    /// <summary>
    /// Gets the current VPN server configuration.
    /// </summary>
    Task<VpnServerConfig> GetServerConfigAsync();

    /// <summary>
    /// Updates the VPN server configuration.
    /// </summary>
    Task UpdateServerConfigAsync(VpnServerConfig config);

    /// <summary>
    /// Checks if the VPN server is properly configured.
    /// </summary>
    Task<bool> IsConfiguredAsync();

    /// <summary>
    /// Reads the server public key from the WireGuard container.
    /// </summary>
    Task<string?> GetServerPublicKeyFromContainerAsync();
}

/// <summary>
/// VPN server configuration.
/// </summary>
public class VpnServerConfig
{
    public string? ServerPublicKey { get; set; }
    public string? ServerEndpoint { get; set; }
    public int ServerPort { get; set; } = 51820;
    public string AllowedIPs { get; set; } = "0.0.0.0/0";
    public string DNS { get; set; } = "10.8.0.1";
    public string Subnet { get; set; } = "10.8.0.0/24";
    public bool IsConfigured => !string.IsNullOrEmpty(ServerPublicKey) && !string.IsNullOrEmpty(ServerEndpoint);
}
