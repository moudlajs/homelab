using System.Net.Http.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;


namespace HomeLab.Cli.Services.Traefik;

/// <summary>
/// Traefik reverse proxy client implementation.
/// </summary>
public class TraefikClient : ITraefikClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string ServiceName => "Traefik";

    public TraefikClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;
        var config = configService.GetServiceConfig("traefik");
        _baseUrl = config.Url ?? "http://localhost:8080";
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/overview");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        var isHealthy = await IsHealthyAsync();
        var overview = await GetOverviewAsync();

        return new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = isHealthy,
            Status = isHealthy ? "Running" : "Unavailable",
            Message = isHealthy ? "Traefik is accessible" : "Traefik API is not accessible",
            Metrics = new Dictionary<string, string>
            {
                { "Routers", overview.TotalRouters.ToString() },
                { "Services", overview.TotalServices.ToString() },
                { "Middlewares", overview.TotalMiddlewares.ToString() }
            }
        };
    }

    public async Task<List<TraefikRoute>> GetRoutesAsync()
    {
        try
        {
            // Traefik API: /api/http/routers
            var routes = await _httpClient.GetFromJsonAsync<List<TraefikRoute>>($"{_baseUrl}/api/http/routers");
            return routes ?? new List<TraefikRoute>();
        }
        catch
        {
            return new List<TraefikRoute>();
        }
    }

    public async Task<List<TraefikService>> GetServicesAsync()
    {
        try
        {
            // Traefik API: /api/http/services
            var services = await _httpClient.GetFromJsonAsync<List<TraefikService>>($"{_baseUrl}/api/http/services");
            return services ?? new List<TraefikService>();
        }
        catch
        {
            return new List<TraefikService>();
        }
    }

    public async Task<List<TraefikMiddleware>> GetMiddlewaresAsync()
    {
        try
        {
            // Traefik API: /api/http/middlewares
            var middlewares = await _httpClient.GetFromJsonAsync<List<TraefikMiddleware>>($"{_baseUrl}/api/http/middlewares");
            return middlewares ?? new List<TraefikMiddleware>();
        }
        catch
        {
            return new List<TraefikMiddleware>();
        }
    }

    public async Task<TraefikOverview> GetOverviewAsync()
    {
        try
        {
            // Traefik API: /api/overview
            var overview = await _httpClient.GetFromJsonAsync<TraefikOverview>($"{_baseUrl}/api/overview");
            return overview ?? new TraefikOverview();
        }
        catch
        {
            return new TraefikOverview();
        }
    }
}
