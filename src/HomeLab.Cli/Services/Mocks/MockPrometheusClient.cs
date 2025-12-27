using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of Prometheus client for testing.
/// </summary>
public class MockPrometheusClient : IPrometheusClient
{
    public string ServiceName => "Prometheus (Mock)";

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
                { "Version", "2.45.0 (mock)" },
                { "Targets", "5" },
                { "Active Alerts", "1" }
            }
        });
    }

    public Task<List<AlertInfo>> GetActiveAlertsAsync()
    {
        var alerts = new List<AlertInfo>
        {
            new()
            {
                Name = "HighMemoryUsage",
                State = "firing",
                Severity = "warning",
                Summary = "Memory usage above 80%",
                Description = "Node memory usage is at 85% for the last 5 minutes",
                ActiveAt = DateTime.UtcNow.AddMinutes(-15),
                Labels = new Dictionary<string, string>
                {
                    { "instance", "localhost:9100" },
                    { "job", "node-exporter" }
                }
            }
        };

        return Task.FromResult(alerts);
    }

    public Task<List<TargetInfo>> GetTargetsAsync()
    {
        var targets = new List<TargetInfo>
        {
            new()
            {
                Job = "prometheus",
                Instance = "localhost:9090",
                Health = "up",
                LastScrape = DateTime.UtcNow.AddSeconds(-10),
                ScrapeDuration = 0.025
            },
            new()
            {
                Job = "node-exporter",
                Instance = "node-exporter:9100",
                Health = "up",
                LastScrape = DateTime.UtcNow.AddSeconds(-12),
                ScrapeDuration = 0.018
            },
            new()
            {
                Job = "docker",
                Instance = "host.docker.internal:9323",
                Health = "down",
                LastScrape = DateTime.UtcNow.AddSeconds(-30),
                Error = "context deadline exceeded"
            }
        };

        return Task.FromResult(targets);
    }

    public Task<string> QueryAsync(string query)
    {
        return Task.FromResult($"Mock result for query: {query}");
    }
}
