using FluentAssertions;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.EventLog;
using Xunit;

namespace HomeLab.Cli.Tests.Services.EventLog;

public class EventLogServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testLogPath;
    private readonly EventLogService _sut;

    public EventLogServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"homelab-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testLogPath = Path.Combine(_testDir, "events.jsonl");
        _sut = new EventLogService(_testLogPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteEventAsync_CreatesFileAndAppendsEntry()
    {
        var entry = CreateEntry(DateTime.UtcNow, cpu: 25, mem: 50);

        await _sut.WriteEventAsync(entry);

        File.Exists(_testLogPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(_testLogPath);
        lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteEventAsync_AppendsMultipleEntries()
    {
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow, cpu: 10, mem: 40));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow, cpu: 20, mem: 50));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow, cpu: 30, mem: 60));

        var lines = await File.ReadAllLinesAsync(_testLogPath);
        lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task ReadEventsAsync_ReturnsEmptyWhenNoFile()
    {
        var events = await _sut.ReadEventsAsync();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadEventsAsync_ReturnsAllEntries()
    {
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow.AddMinutes(-10), cpu: 10, mem: 40));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow.AddMinutes(-5), cpu: 20, mem: 50));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow, cpu: 30, mem: 60));

        var events = await _sut.ReadEventsAsync();
        events.Should().HaveCount(3);
    }

    [Fact]
    public async Task ReadEventsAsync_FiltersBySince()
    {
        var now = DateTime.UtcNow;
        await _sut.WriteEventAsync(CreateEntry(now.AddHours(-2), cpu: 10, mem: 40));
        await _sut.WriteEventAsync(CreateEntry(now.AddMinutes(-30), cpu: 20, mem: 50));
        await _sut.WriteEventAsync(CreateEntry(now, cpu: 30, mem: 60));

        var events = await _sut.ReadEventsAsync(since: now.AddHours(-1));
        events.Should().HaveCount(2);
        events[0].System!.CpuPercent.Should().Be(20);
    }

    [Fact]
    public async Task ReadEventsAsync_FiltersByUntil()
    {
        var now = DateTime.UtcNow;
        await _sut.WriteEventAsync(CreateEntry(now.AddHours(-2), cpu: 10, mem: 40));
        await _sut.WriteEventAsync(CreateEntry(now.AddMinutes(-30), cpu: 20, mem: 50));
        await _sut.WriteEventAsync(CreateEntry(now, cpu: 30, mem: 60));

        var events = await _sut.ReadEventsAsync(until: now.AddHours(-1));
        events.Should().HaveCount(1);
        events[0].System!.CpuPercent.Should().Be(10);
    }

    [Fact]
    public async Task ReadEventsAsync_SkipsMalformedLines()
    {
        await File.WriteAllTextAsync(_testLogPath,
            "{\"timestamp\":\"2026-02-14T12:00:00Z\",\"system\":{\"cpuPercent\":10,\"memoryPercent\":40,\"diskPercent\":20,\"uptime\":\"1 day\"}}\n" +
            "NOT VALID JSON\n" +
            "{\"timestamp\":\"2026-02-14T13:00:00Z\",\"system\":{\"cpuPercent\":20,\"memoryPercent\":50,\"diskPercent\":20,\"uptime\":\"1 day\"}}\n");

        var events = await _sut.ReadEventsAsync();
        events.Should().HaveCount(2);
    }

    [Fact]
    public async Task CleanupAsync_RemovesOldEntries()
    {
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow.AddDays(-10), cpu: 10, mem: 40));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow.AddDays(-3), cpu: 20, mem: 50));
        await _sut.WriteEventAsync(CreateEntry(DateTime.UtcNow, cpu: 30, mem: 60));

        await _sut.CleanupAsync(retentionDays: 7);

        var events = await _sut.ReadEventsAsync();
        events.Should().HaveCount(2);
        events[0].System!.CpuPercent.Should().Be(20);
    }

    [Fact]
    public async Task CleanupAsync_NoOpWhenNoFile()
    {
        // Should not throw
        await _sut.CleanupAsync();
    }

    [Fact]
    public async Task RoundTrip_PreservesAllFields()
    {
        var entry = new EventLogEntry
        {
            Timestamp = DateTime.UtcNow,
            System = new SystemSnapshot { CpuPercent = 42.5, MemoryPercent = 65, DiskPercent = 30, Uptime = "3 days" },
            Power = new PowerSnapshot
            {
                RecentEvents = new List<PowerEvent>
                {
                    new() { Timestamp = DateTime.UtcNow, Type = "Wake" }
                }
            },
            Tailscale = new TailscaleSnapshot
            {
                IsConnected = true,
                BackendState = "Running",
                SelfIp = "100.126.50.127",
                PeerCount = 3,
                OnlinePeerCount = 2
            },
            Docker = new DockerSnapshot
            {
                Available = true,
                RunningCount = 5,
                TotalCount = 7,
                Containers = new List<ContainerBrief>
                {
                    new() { Name = "prometheus", IsRunning = true },
                    new() { Name = "grafana", IsRunning = false }
                }
            },
            Network = new NetworkSnapshot { DeviceCount = 12 },
            Services = new List<ServiceHealthEntry>
            {
                new() { Name = "Prometheus", IsHealthy = true }
            },
            Errors = new List<string> { "test warning" }
        };

        await _sut.WriteEventAsync(entry);
        var events = await _sut.ReadEventsAsync();

        events.Should().HaveCount(1);
        var result = events[0];
        result.System!.CpuPercent.Should().Be(42.5);
        result.System.MemoryPercent.Should().Be(65);
        result.Tailscale!.IsConnected.Should().BeTrue();
        result.Tailscale.SelfIp.Should().Be("100.126.50.127");
        result.Tailscale.OnlinePeerCount.Should().Be(2);
        result.Docker!.RunningCount.Should().Be(5);
        result.Docker.Containers.Should().HaveCount(2);
        result.Network!.DeviceCount.Should().Be(12);
        result.Services.Should().HaveCount(1);
        result.Power!.RecentEvents.Should().HaveCount(1);
        result.Errors.Should().ContainSingle("test warning");
    }

    private static EventLogEntry CreateEntry(DateTime timestamp, double cpu, double mem)
    {
        return new EventLogEntry
        {
            Timestamp = timestamp,
            System = new SystemSnapshot
            {
                CpuPercent = cpu,
                MemoryPercent = mem,
                DiskPercent = 20,
                Uptime = "1 day"
            }
        };
    }
}
