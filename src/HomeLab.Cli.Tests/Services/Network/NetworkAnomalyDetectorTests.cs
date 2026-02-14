using FluentAssertions;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Network;
using Xunit;

namespace HomeLab.Cli.Tests.Services.Network;

public class NetworkAnomalyDetectorTests
{
    private readonly NetworkAnomalyDetector _sut = new();

    private static EventLogEntry CreateEntry(
        DateTime? timestamp = null,
        string[]? deviceIps = null,
        long trafficBytes = 0,
        int criticalAlerts = 0,
        int highAlerts = 0)
    {
        var entry = new EventLogEntry
        {
            Timestamp = timestamp ?? DateTime.UtcNow,
            Network = new NetworkSnapshot
            {
                DeviceCount = deviceIps?.Length ?? 0,
                Devices = (deviceIps ?? Array.Empty<string>())
                    .Select(ip => new DeviceBrief { Ip = ip })
                    .ToList()
            }
        };

        if (trafficBytes > 0)
        {
            entry.Network.Traffic = new TrafficSummary { TotalBytes = trafficBytes };
        }

        if (criticalAlerts > 0 || highAlerts > 0)
        {
            entry.Network.Security = new SecuritySummary
            {
                TotalAlerts = criticalAlerts + highAlerts,
                CriticalCount = criticalAlerts,
                HighCount = highAlerts,
                RecentAlerts = new List<AlertBrief>()
            };

            if (criticalAlerts > 0)
            {
                entry.Network.Security.RecentAlerts.Add(new AlertBrief
                {
                    Severity = "critical",
                    Signature = "Test Critical Alert",
                    SourceIp = "10.0.0.1",
                    DestinationIp = "192.168.1.1"
                });
            }

            if (highAlerts > 0)
            {
                entry.Network.Security.RecentAlerts.Add(new AlertBrief
                {
                    Severity = "high",
                    Signature = "Test High Alert",
                    SourceIp = "10.0.0.2",
                    DestinationIp = "192.168.1.1"
                });
            }
        }

        return entry;
    }

    [Fact]
    public void DetectAnomalies_SingleEvent_ReturnsEmpty()
    {
        var events = new List<EventLogEntry> { CreateEntry(deviceIps: new[] { "192.168.1.1" }) };
        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().BeEmpty();
    }

    [Fact]
    public void DetectAnomalies_NoChanges_ReturnsEmpty()
    {
        var ips = new[] { "192.168.1.1", "192.168.1.2" };
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: ips, trafficBytes: 1000),
            CreateEntry(deviceIps: ips, trafficBytes: 1100)
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().BeEmpty();
    }

    [Fact]
    public void DetectAnomalies_NewDevice_ReturnsNewDeviceAnomaly()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: new[] { "192.168.1.1", "192.168.1.2" }),
            CreateEntry(deviceIps: new[] { "192.168.1.1", "192.168.1.2", "192.168.1.99" })
        };

        var anomalies = _sut.DetectAnomalies(events);

        anomalies.Should().ContainSingle(a => a.Type == "NewDevice");
        var newDevice = anomalies.First(a => a.Type == "NewDevice");
        newDevice.Severity.Should().Be("warning");
        newDevice.Details["ip"].Should().Be("192.168.1.99");
    }

    [Fact]
    public void DetectAnomalies_DeviceGone_AfterConsecutivePresence_ReturnsAnomaly()
    {
        var allIps = new[] { "192.168.1.1", "192.168.1.2" };
        var missingIp = new[] { "192.168.1.1" };

        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: allIps),
            CreateEntry(deviceIps: allIps),
            CreateEntry(deviceIps: allIps),
            CreateEntry(deviceIps: allIps),
            CreateEntry(deviceIps: missingIp) // .2 disappears after 4 consecutive
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().Contain(a => a.Type == "DeviceGone");
    }

    [Fact]
    public void DetectAnomalies_DeviceGone_ShortPresence_NoAnomaly()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: new[] { "192.168.1.1", "192.168.1.2" }),
            CreateEntry(deviceIps: new[] { "192.168.1.1" }) // .2 only seen once, doesn't trigger
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().NotContain(a => a.Type == "DeviceGone");
    }

    [Fact]
    public void DetectAnomalies_TrafficSpike_ReturnsWarning()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(trafficBytes: 1000),
            CreateEntry(trafficBytes: 1100),
            CreateEntry(trafficBytes: 900),
            CreateEntry(trafficBytes: 1050),
            CreateEntry(trafficBytes: 5000) // 5x the average ~ spike
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().Contain(a => a.Type == "TrafficSpike");
    }

    [Fact]
    public void DetectAnomalies_CriticalSecurityAlert_ReturnsCriticalAnomaly()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: new[] { "192.168.1.1" }),
            CreateEntry(deviceIps: new[] { "192.168.1.1" }, criticalAlerts: 2)
        };

        var anomalies = _sut.DetectAnomalies(events);
        var alert = anomalies.FirstOrDefault(a => a.Type == "SecurityAlert");
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be("critical");
    }

    [Fact]
    public void DetectAnomalies_HighSecurityAlert_ReturnsWarningAnomaly()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: new[] { "192.168.1.1" }),
            CreateEntry(deviceIps: new[] { "192.168.1.1" }, highAlerts: 3)
        };

        var anomalies = _sut.DetectAnomalies(events);
        var alert = anomalies.FirstOrDefault(a => a.Type == "SecurityAlert");
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be("warning");
    }

    [Fact]
    public void DetectAnomalies_DeviceCountAnomaly_LargeChange_ReturnsWarning()
    {
        var events = new List<EventLogEntry>
        {
            CreateEntry(deviceIps: Enumerable.Range(1, 10).Select(i => $"192.168.1.{i}").ToArray()),
            CreateEntry(deviceIps: Enumerable.Range(1, 10).Select(i => $"192.168.1.{i}").ToArray()),
            CreateEntry(deviceIps: Enumerable.Range(1, 10).Select(i => $"192.168.1.{i}").ToArray()),
            CreateEntry(deviceIps: Enumerable.Range(1, 10).Select(i => $"192.168.1.{i}").ToArray()),
            CreateEntry(deviceIps: Enumerable.Range(1, 20).Select(i => $"192.168.1.{i}").ToArray()) // doubled
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().Contain(a => a.Type == "DeviceCountAnomaly");
    }

    [Fact]
    public void DetectAnomalies_NullNetworkSnapshot_HandlesGracefully()
    {
        var events = new List<EventLogEntry>
        {
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-10) },
            new() { Timestamp = DateTime.UtcNow }
        };

        var anomalies = _sut.DetectAnomalies(events);
        anomalies.Should().BeEmpty();
    }

}
