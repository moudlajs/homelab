using HomeLab.Cli.Services.AdGuard;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Grafana;
using HomeLab.Cli.Services.HomeAssistant;
using HomeLab.Cli.Services.Mocks;
using HomeLab.Cli.Services.Prometheus;
using HomeLab.Cli.Services.Speedtest;
using HomeLab.Cli.Services.Traefik;
using HomeLab.Cli.Services.UptimeKuma;
using HomeLab.Cli.Services.WireGuard;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Factory that creates service clients based on configuration.
/// Returns mock clients if development.use_mock_services = true.
/// </summary>
public class ServiceClientFactory : IServiceClientFactory
{
    private readonly IHomelabConfigService _configService;
    private readonly HttpClient _httpClient;

    public ServiceClientFactory(IHomelabConfigService configService, HttpClient httpClient)
    {
        _configService = configService;
        _httpClient = httpClient;
    }

    public IAdGuardClient CreateAdGuardClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockAdGuardClient();
        }

        return new AdGuardClient(_configService, _httpClient);
    }

    public IWireGuardClient CreateWireGuardClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockWireGuardClient();
        }

        // Real WireGuardClient implementation (Day 3)
        return new WireGuardClient(_configService);
    }

    public IPrometheusClient CreatePrometheusClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockPrometheusClient();
        }

        return new PrometheusClient(_configService, _httpClient);
    }

    public IGrafanaClient CreateGrafanaClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockGrafanaClient();
        }

        return new GrafanaClient(_configService, _httpClient);
    }

    public UptimeKumaClient CreateUptimeKumaClient()
    {
        // For now, always return real client (uses mock data internally if service unavailable)
        // TODO: Add uptime-kuma URL to config file
        var baseUrl = "http://localhost:3001";
        return new UptimeKumaClient(_httpClient, baseUrl);
    }

    public SpeedtestClient CreateSpeedtestClient()
    {
        // For now, always return real client (uses mock data internally if service unavailable)
        // TODO: Add speedtest-tracker URL to config file
        var baseUrl = "http://localhost:8080";
        return new SpeedtestClient(_httpClient, baseUrl);
    }

    public IHomeAssistantClient CreateHomeAssistantClient()
    {
        // Always return real client (uses mock data internally if service unavailable)
        return new HomeAssistantClient(_httpClient, _configService);
    }

    public ITraefikClient CreateTraefikClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockTraefikClient();
        }

        return new TraefikClient(_configService, _httpClient);
    }
}
