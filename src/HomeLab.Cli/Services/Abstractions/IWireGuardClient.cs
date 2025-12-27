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
}
