namespace HomeLab.Cli.Models;

/// <summary>
/// Represents the overall Tailscale connection status.
/// Parsed from `tailscale status --json` output.
/// </summary>
public class TailscaleStatus
{
    public string BackendState { get; set; } = "Stopped";
    public string? TailnetName { get; set; }
    public string? MagicDNSSuffix { get; set; }
    public TailscaleDevice? Self { get; set; }
    public List<TailscaleDevice> Peers { get; set; } = new();
    public bool IsConnected => BackendState == "Running";
}

/// <summary>
/// Represents a device on the Tailscale tailnet (self or peer).
/// </summary>
public class TailscaleDevice
{
    public string Id { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string DNSName { get; set; } = string.Empty;
    public string OS { get; set; } = string.Empty;
    public List<string> TailscaleIPs { get; set; } = new();
    public bool Online { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool ExitNode { get; set; }
    public bool ExitNodeOption { get; set; }

    public string? PrimaryIP => TailscaleIPs.FirstOrDefault();
}
