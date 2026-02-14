using FluentAssertions;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.AI;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Network;
using Moq;
using Xunit;

namespace HomeLab.Cli.Tests.Services.EventLog;

public class SystemDataCollectorEventHistoryTests
{
    private readonly SystemDataCollector _sut;

    public SystemDataCollectorEventHistoryTests()
    {
        _sut = new SystemDataCollector(
            Mock.Of<IDockerService>(),
            Mock.Of<IServiceClientFactory>(),
            Mock.Of<INmapService>());
    }

    [Fact]
    public void FormatAsPrompt_WithoutEvents_DoesNotIncludeHistorySection()
    {
        var snapshot = CreateSnapshot();

        var result = _sut.FormatAsPrompt(snapshot);

        result.Should().NotContain("EVENT HISTORY");
    }

    [Fact]
    public void FormatAsPrompt_WithEvents_IncludesHistorySection()
    {
        var snapshot = CreateSnapshot();
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEvent(DateTime.UtcNow.AddHours(-1)),
            CreateEvent(DateTime.UtcNow)
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("=== EVENT HISTORY ===");
        result.Should().Contain("Total snapshots: 2");
    }

    [Fact]
    public void FormatAsPrompt_DetectsGaps()
    {
        var snapshot = CreateSnapshot();
        var now = DateTime.UtcNow;
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEvent(now.AddHours(-3)),
            CreateEvent(now.AddHours(-2).AddMinutes(-55)), // 5 min gap - no gap
            CreateEvent(now), // 2h55m gap - detected!
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("Gaps detected (1)");
        result.Should().Contain("possible sleep/outage");
    }

    [Fact]
    public void FormatAsPrompt_IncludesPowerEvents()
    {
        var snapshot = CreateSnapshot();
        var now = DateTime.UtcNow;
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEventWithPower(now.AddHours(-1), "Sleep"),
            CreateEventWithPower(now, "Wake")
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("Power events (2)");
        result.Should().Contain("Sleep");
        result.Should().Contain("Wake");
    }

    [Fact]
    public void FormatAsPrompt_IncludesTailscaleConnectivity()
    {
        var snapshot = CreateSnapshot();
        var now = DateTime.UtcNow;
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEventWithTailscale(now.AddMinutes(-15), connected: true),
            CreateEventWithTailscale(now.AddMinutes(-10), connected: false),
            CreateEventWithTailscale(now.AddMinutes(-5), connected: false),
            CreateEventWithTailscale(now, connected: true)
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("Tailscale: connected 50% of time, 1 disconnection(s)");
    }

    [Fact]
    public void FormatAsPrompt_TracksDockerChanges()
    {
        var snapshot = CreateSnapshot();
        var now = DateTime.UtcNow;
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEventWithDocker(now.AddMinutes(-10), new[] { ("prometheus", true), ("grafana", true) }),
            CreateEventWithDocker(now, new[] { ("prometheus", true), ("grafana", false) })
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("Docker changes (1)");
        result.Should().Contain("grafana stopped");
    }

    [Fact]
    public void FormatAsPrompt_TracksServiceHealthChanges()
    {
        var snapshot = CreateSnapshot();
        var now = DateTime.UtcNow;
        var events = new List<Models.EventLog.EventLogEntry>
        {
            CreateEventWithHealth(now.AddMinutes(-10), new[] { ("Prometheus", true) }),
            CreateEventWithHealth(now, new[] { ("Prometheus", false) })
        };

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().Contain("Service health changes (1)");
        result.Should().Contain("Prometheus down");
    }

    [Fact]
    public void FormatAsPrompt_EmptyEventList_DoesNotIncludeHistory()
    {
        var snapshot = CreateSnapshot();
        var events = new List<Models.EventLog.EventLogEntry>();

        var result = _sut.FormatAsPrompt(snapshot, events);

        result.Should().NotContain("EVENT HISTORY");
    }

    private static HomelabDataSnapshot CreateSnapshot()
    {
        return new HomelabDataSnapshot
        {
            System = new SystemMetrics
            {
                CpuCount = 8,
                CpuUsagePercent = 25,
                TotalMemoryGB = 16,
                UsedMemoryGB = 8,
                MemoryUsagePercent = 50,
                DiskTotal = "460Gi",
                DiskUsed = "110Gi",
                DiskAvailable = "350Gi",
                DiskUsagePercent = 24,
                Uptime = "3 days"
            }
        };
    }

    private static Models.EventLog.EventLogEntry CreateEvent(DateTime timestamp)
    {
        return new Models.EventLog.EventLogEntry
        {
            Timestamp = timestamp,
            System = new SystemSnapshot { CpuPercent = 25, MemoryPercent = 50, DiskPercent = 24, Uptime = "3 days" }
        };
    }

    private static Models.EventLog.EventLogEntry CreateEventWithPower(DateTime timestamp, string type)
    {
        var entry = CreateEvent(timestamp);
        entry.Power = new PowerSnapshot
        {
            RecentEvents = new List<PowerEvent>
            {
                new() { Timestamp = timestamp, Type = type }
            }
        };
        return entry;
    }

    private static Models.EventLog.EventLogEntry CreateEventWithTailscale(DateTime timestamp, bool connected)
    {
        var entry = CreateEvent(timestamp);
        entry.Tailscale = new TailscaleSnapshot
        {
            IsConnected = connected,
            BackendState = connected ? "Running" : "Stopped",
            PeerCount = 2,
            OnlinePeerCount = connected ? 1 : 0
        };
        return entry;
    }

    private static Models.EventLog.EventLogEntry CreateEventWithDocker(DateTime timestamp, (string name, bool running)[] containers)
    {
        var entry = CreateEvent(timestamp);
        entry.Docker = new DockerSnapshot
        {
            Available = true,
            TotalCount = containers.Length,
            RunningCount = containers.Count(c => c.running),
            Containers = containers.Select(c => new ContainerBrief { Name = c.name, IsRunning = c.running }).ToList()
        };
        return entry;
    }

    private static Models.EventLog.EventLogEntry CreateEventWithHealth(DateTime timestamp, (string name, bool healthy)[] services)
    {
        var entry = CreateEvent(timestamp);
        entry.Services = services.Select(s => new ServiceHealthEntry { Name = s.name, IsHealthy = s.healthy }).ToList();
        return entry;
    }
}
