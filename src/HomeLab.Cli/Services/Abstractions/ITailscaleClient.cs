using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for Tailscale VPN operations.
/// Wraps the tailscale CLI binary.
/// </summary>
public interface ITailscaleClient : IServiceClient
{
    Task<TailscaleStatus> GetStatusAsync();
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<string?> GetTailscaleIPAsync();
    Task<bool> IsTailscaleInstalledAsync();
    Task<string?> GetVersionAsync();
}
