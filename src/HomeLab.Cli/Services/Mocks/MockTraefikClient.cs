using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of Traefik client for testing.
/// </summary>
public class MockTraefikClient : ITraefikClient
{
    public string ServiceName => "Traefik (Mock)";

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        return Task.FromResult(new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = "Running",
            Message = "Mock service - always healthy",
            Metrics = new Dictionary<string, string>
            {
                { "Routers", "3" },
                { "Services", "3" },
                { "Middlewares", "4" }
            }
        });
    }

    public Task<List<TraefikRoute>> GetRoutesAsync()
    {
        return Task.FromResult(new List<TraefikRoute>
        {
            new()
            {
                Name = "adguard-http",
                Rule = "Host(`dns.homelab.local`)",
                Service = "adguard-service",
                EntryPoint = "web",
                Status = "enabled",
                Middlewares = new List<string> { "auth-basic", "compress" },
                TLS = false
            },
            new()
            {
                Name = "grafana-https",
                Rule = "Host(`grafana.homelab.local`)",
                Service = "grafana-service",
                EntryPoint = "websecure",
                Status = "enabled",
                Middlewares = new List<string> { "redirect-https", "headers-secure" },
                TLS = true
            },
            new()
            {
                Name = "prometheus-https",
                Rule = "Host(`prometheus.homelab.local`) && PathPrefix(`/metrics`)",
                Service = "prometheus-service",
                EntryPoint = "websecure",
                Status = "enabled",
                Middlewares = new List<string> { "auth-basic" },
                TLS = true
            }
        });
    }

    public Task<List<TraefikService>> GetServicesAsync()
    {
        return Task.FromResult(new List<TraefikService>
        {
            new()
            {
                Name = "adguard-service",
                Type = "loadbalancer",
                Servers = new List<string> { "http://172.20.0.2:3000" },
                Status = "healthy",
                LoadBalancer = "round-robin"
            },
            new()
            {
                Name = "grafana-service",
                Type = "loadbalancer",
                Servers = new List<string> { "http://172.20.0.3:3001", "http://172.20.0.4:3001" },
                Status = "healthy",
                LoadBalancer = "weighted"
            },
            new()
            {
                Name = "prometheus-service",
                Type = "loadbalancer",
                Servers = new List<string> { "http://172.20.0.5:9090" },
                Status = "healthy",
                LoadBalancer = "round-robin"
            }
        });
    }

    public Task<List<TraefikMiddleware>> GetMiddlewaresAsync()
    {
        return Task.FromResult(new List<TraefikMiddleware>
        {
            new()
            {
                Name = "auth-basic",
                Type = "basicAuth",
                Status = "enabled",
                Config = new Dictionary<string, string>
                {
                    { "users", "admin:$apr1$..." },
                    { "realm", "Homelab" }
                }
            },
            new()
            {
                Name = "redirect-https",
                Type = "redirectScheme",
                Status = "enabled",
                Config = new Dictionary<string, string>
                {
                    { "scheme", "https" },
                    { "permanent", "true" }
                }
            },
            new()
            {
                Name = "compress",
                Type = "compress",
                Status = "enabled",
                Config = new Dictionary<string, string>
                {
                    { "minResponseBodyBytes", "1024" }
                }
            },
            new()
            {
                Name = "headers-secure",
                Type = "headers",
                Status = "enabled",
                Config = new Dictionary<string, string>
                {
                    { "stsSeconds", "31536000" },
                    { "stsIncludeSubdomains", "true" }
                }
            }
        });
    }

    public Task<TraefikOverview> GetOverviewAsync()
    {
        return Task.FromResult(new TraefikOverview
        {
            TotalRouters = 3,
            TotalServices = 3,
            TotalMiddlewares = 4,
            HealthyRouters = 3,
            HealthyServices = 3,
            EntryPoints = new List<string> { "web", "websecure" }
        });
    }
}
