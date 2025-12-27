namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a WireGuard VPN peer.
/// </summary>
public class VpnPeer
{
    public string Name { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string AllowedIPs { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public DateTime? LastHandshake { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public bool IsActive { get; set; }
}
