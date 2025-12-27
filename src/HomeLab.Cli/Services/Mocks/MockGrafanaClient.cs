using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of Grafana client for testing.
/// </summary>
public class MockGrafanaClient : IGrafanaClient
{
    public string ServiceName => "Grafana (Mock)";

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
                { "Version", "10.0.0 (mock)" },
                { "Dashboards", "8" }
            }
        });
    }

    public Task<List<DashboardInfo>> GetDashboardsAsync()
    {
        var dashboards = new List<DashboardInfo>
        {
            new()
            {
                Uid = "node-exporter",
                Title = "Node Exporter Full",
                Uri = "db/node-exporter",
                Url = "/d/node-exporter/node-exporter-full",
                Tags = new List<string> { "system", "node" },
                IsStarred = true
            },
            new()
            {
                Uid = "docker",
                Title = "Docker Container Metrics",
                Uri = "db/docker",
                Url = "/d/docker/docker-container-metrics",
                Tags = new List<string> { "docker" },
                IsStarred = false
            },
            new()
            {
                Uid = "homelab",
                Title = "HomeLab Overview",
                Uri = "db/homelab",
                Url = "/d/homelab/homelab-overview",
                Tags = new List<string> { "overview" },
                IsStarred = true
            }
        };

        return Task.FromResult(dashboards);
    }

    public Task OpenDashboardAsync(string uid)
    {
        var url = GetDashboardUrl(uid);
        Console.WriteLine($"[Mock] Would open: {url}");
        return Task.CompletedTask;
    }

    public string GetDashboardUrl(string uid = "")
    {
        var baseUrl = "http://localhost:3001";
        return string.IsNullOrEmpty(uid) ? baseUrl : $"{baseUrl}/d/{uid}";
    }
}
