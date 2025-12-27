using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of AdGuard client for testing.
/// Returns fake data without making real API calls.
/// </summary>
public class MockAdGuardClient : IAdGuardClient
{
    public string ServiceName => "AdGuard Home (Mock)";

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
                { "Version", "v0.107.0 (mock)" },
                { "Uptime", "5d 12h" }
            }
        });
    }

    public Task<DnsStats> GetStatsAsync()
    {
        var random = new Random();
        var totalQueries = random.Next(50000, 100000);
        var blockedQueries = random.Next(10000, 25000);

        return Task.FromResult(new DnsStats
        {
            TotalQueries = totalQueries,
            BlockedQueries = blockedQueries,
            BlockedPercentage = Math.Round((double)blockedQueries / totalQueries * 100, 2),
            SafeBrowsingBlocks = random.Next(100, 500),
            ParentalBlocks = random.Next(0, 50)
        });
    }

    public Task<List<BlockedDomain>> GetTopBlockedDomainsAsync(int limit = 10)
    {
        var domains = new List<BlockedDomain>
        {
            new() { Domain = "doubleclick.net", Count = 3421 },
            new() { Domain = "google-analytics.com", Count = 2876 },
            new() { Domain = "facebook.com", Count = 1543 },
            new() { Domain = "googletagmanager.com", Count = 1289 },
            new() { Domain = "ads.youtube.com", Count = 987 },
            new() { Domain = "pixel.facebook.com", Count = 654 },
            new() { Domain = "tracking.example.com", Count = 432 },
            new() { Domain = "analytics.twitter.com", Count = 321 },
            new() { Domain = "ad.adserver.com", Count = 234 },
            new() { Domain = "telemetry.microsoft.com", Count = 156 }
        };

        return Task.FromResult(domains.Take(limit).ToList());
    }

    public Task UpdateFiltersAsync()
    {
        // Simulate filter update delay
        return Task.Delay(500);
    }
}
