namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Factory for creating service clients (real or mock implementations).
/// </summary>
public interface IServiceClientFactory
{
    /// <summary>
    /// Creates an AdGuard client.
    /// </summary>
    IAdGuardClient CreateAdGuardClient();

    /// <summary>
    /// Creates a WireGuard client.
    /// </summary>
    IWireGuardClient CreateWireGuardClient();

    /// <summary>
    /// Creates a Prometheus client.
    /// </summary>
    IPrometheusClient CreatePrometheusClient();

    /// <summary>
    /// Creates a Grafana client.
    /// </summary>
    IGrafanaClient CreateGrafanaClient();

    /// <summary>
    /// Creates an Uptime Kuma client (Phase 6).
    /// </summary>
    UptimeKuma.UptimeKumaClient CreateUptimeKumaClient();

    /// <summary>
    /// Creates a Speedtest Tracker client (Phase 6).
    /// </summary>
    Speedtest.SpeedtestClient CreateSpeedtestClient();

    /// <summary>
    /// Creates a Home Assistant client (Phase 8).
    /// </summary>
    HomeAssistant.IHomeAssistantClient CreateHomeAssistantClient();
}
