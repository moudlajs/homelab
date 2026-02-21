using HomeLab.Cli.Services.AdGuard;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.HomeAssistant;
using HomeLab.Cli.Services.Ntopng;
using HomeLab.Cli.Services.Suricata;
using HomeLab.Cli.Services.Tailscale;
using HomeLab.Cli.Services.Traefik;
using HomeLab.Cli.Services.UptimeKuma;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Factory that creates service clients based on configuration.
/// </summary>
public class ServiceClientFactory : IServiceClientFactory
{
    private readonly IHomelabConfigService _configService;
    private readonly HttpClient _httpClient;
    private readonly IDockerService _dockerService;

    public ServiceClientFactory(IHomelabConfigService configService, HttpClient httpClient, IDockerService dockerService)
    {
        _configService = configService;
        _httpClient = httpClient;
        _dockerService = dockerService;
    }

    public IAdGuardClient CreateAdGuardClient()
    {
        return new AdGuardClient(_configService, _httpClient);
    }

    public UptimeKumaClient CreateUptimeKumaClient()
    {
        var config = _configService.GetServiceConfig("uptime_kuma");
        var baseUrl = config?.Url ?? "http://localhost:3001";
        return new UptimeKumaClient(_httpClient, baseUrl);
    }

    public IHomeAssistantClient CreateHomeAssistantClient()
    {
        return new HomeAssistantClient(_httpClient, _configService);
    }

    public ITraefikClient CreateTraefikClient()
    {
        return new TraefikClient(_configService, _httpClient);
    }

    public INtopngClient CreateNtopngClient()
    {
        return new NtopngClient(_configService, _httpClient);
    }

    public ISuricataClient CreateSuricataClient()
    {
        return new SuricataClient(_configService);
    }

    public ITailscaleClient CreateTailscaleClient()
    {
        return new TailscaleClient();
    }
}
