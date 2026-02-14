using FluentAssertions;
using HomeLab.Cli.Models;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.EventLog;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Network;
using Moq;
using Xunit;
using IServiceHealthCheckService = HomeLab.Cli.Services.Health.IServiceHealthCheckService;
using ServiceHealthResult = HomeLab.Cli.Services.Health.ServiceHealthResult;

namespace HomeLab.Cli.Tests.Services.EventLog;

public class EventCollectorTests
{
    private readonly Mock<IDockerService> _mockDocker;
    private readonly Mock<IServiceClientFactory> _mockClientFactory;
    private readonly Mock<INmapService> _mockNmap;
    private readonly Mock<IServiceHealthCheckService> _mockHealthCheck;
    private readonly EventCollector _sut;

    public EventCollectorTests()
    {
        _mockDocker = new Mock<IDockerService>();
        _mockClientFactory = new Mock<IServiceClientFactory>();
        _mockNmap = new Mock<INmapService>();
        _mockHealthCheck = new Mock<IServiceHealthCheckService>();

        // Default: Tailscale not installed
        var mockTailscale = new Mock<ITailscaleClient>();
        mockTailscale.Setup(t => t.IsTailscaleInstalledAsync()).ReturnsAsync(false);
        _mockClientFactory.Setup(f => f.CreateTailscaleClient()).Returns(mockTailscale.Object);

        // Default: Docker not available
        _mockDocker.Setup(d => d.IsDockerAvailableAsync()).ReturnsAsync(false);

        // Default: nmap not available
        _mockNmap.Setup(n => n.IsNmapAvailable()).Returns(false);

        // Default: no services
        _mockHealthCheck.Setup(h => h.CheckAllServicesAsync())
            .ReturnsAsync(new List<ServiceHealthResult>());

        _sut = new EventCollector(
            _mockDocker.Object,
            _mockClientFactory.Object,
            _mockNmap.Object,
            _mockHealthCheck.Object);
    }

    [Fact]
    public async Task CollectEventAsync_ReturnsEntryWithTimestamp()
    {
        var before = DateTime.UtcNow;
        var entry = await _sut.CollectEventAsync();
        var after = DateTime.UtcNow;

        entry.Should().NotBeNull();
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CollectEventAsync_SetsSystemSnapshot()
    {
        var entry = await _sut.CollectEventAsync();

        entry.System.Should().NotBeNull();
    }

    [Fact]
    public async Task CollectEventAsync_DockerAvailable_CollectsContainers()
    {
        _mockDocker.Setup(d => d.IsDockerAvailableAsync()).ReturnsAsync(true);
        _mockDocker.Setup(d => d.ListContainersAsync(false))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_adguard", IsRunning = true },
                new() { Name = "homelab_grafana", IsRunning = false }
            });

        var entry = await _sut.CollectEventAsync();

        entry.Docker.Should().NotBeNull();
        entry.Docker!.Available.Should().BeTrue();
        entry.Docker.TotalCount.Should().Be(2);
        entry.Docker.RunningCount.Should().Be(1);
        entry.Docker.Containers.Should().HaveCount(2);
    }

    [Fact]
    public async Task CollectEventAsync_DockerUnavailable_RecordsError()
    {
        _mockDocker.Setup(d => d.IsDockerAvailableAsync()).ReturnsAsync(false);

        var entry = await _sut.CollectEventAsync();

        entry.Docker.Should().NotBeNull();
        entry.Docker!.Available.Should().BeFalse();
        entry.Errors.Should().Contain(e => e.Contains("Docker"));
    }

    [Fact]
    public async Task CollectEventAsync_TailscaleConnected_CollectsStatus()
    {
        var mockTailscale = new Mock<ITailscaleClient>();
        mockTailscale.Setup(t => t.IsTailscaleInstalledAsync()).ReturnsAsync(true);
        mockTailscale.Setup(t => t.GetStatusAsync()).ReturnsAsync(new TailscaleStatus
        {
            BackendState = "Running",
            Self = new TailscaleDevice { TailscaleIPs = new List<string> { "100.1.2.3" } },
            Peers = new List<TailscaleDevice>
            {
                new() { Online = true },
                new() { Online = false }
            }
        });
        _mockClientFactory.Setup(f => f.CreateTailscaleClient()).Returns(mockTailscale.Object);

        var sut = new EventCollector(
            _mockDocker.Object,
            _mockClientFactory.Object,
            _mockNmap.Object,
            _mockHealthCheck.Object);

        var entry = await sut.CollectEventAsync();

        entry.Tailscale.Should().NotBeNull();
        entry.Tailscale!.IsConnected.Should().BeTrue();
        entry.Tailscale.BackendState.Should().Be("Running");
        entry.Tailscale.SelfIp.Should().Be("100.1.2.3");
        entry.Tailscale.PeerCount.Should().Be(2);
        entry.Tailscale.OnlinePeerCount.Should().Be(1);
    }

    [Fact]
    public async Task CollectEventAsync_TailscaleNotInstalled_RecordsError()
    {
        var entry = await _sut.CollectEventAsync();

        entry.Errors.Should().Contain(e => e.Contains("Tailscale"));
    }

    [Fact]
    public async Task CollectEventAsync_NmapAvailable_CollectsNetworkDeviceCount()
    {
        _mockNmap.Setup(n => n.IsNmapAvailable()).Returns(true);
        _mockNmap.Setup(n => n.ScanNetworkAsync("192.168.1.0/24", true))
            .ReturnsAsync(new List<NetworkDevice>
            {
                new() { IpAddress = "192.168.1.1" },
                new() { IpAddress = "192.168.1.100" },
                new() { IpAddress = "192.168.1.200" }
            });

        var entry = await _sut.CollectEventAsync();

        entry.Network.Should().NotBeNull();
        entry.Network!.DeviceCount.Should().Be(3);
    }

    [Fact]
    public async Task CollectEventAsync_NmapUnavailable_DeviceCountZero()
    {
        var entry = await _sut.CollectEventAsync();

        entry.Network.Should().NotBeNull();
        entry.Network!.DeviceCount.Should().Be(0);
    }

    [Fact]
    public async Task CollectEventAsync_HealthCheckReturnsResults_MapsToServiceEntries()
    {
        _mockHealthCheck.Setup(h => h.CheckAllServicesAsync())
            .ReturnsAsync(new List<ServiceHealthResult>
            {
                new() { ServiceName = "adguard", IsHealthy = true },
                new() { ServiceName = "grafana", IsHealthy = false }
            });

        var entry = await _sut.CollectEventAsync();

        entry.Services.Should().HaveCount(2);
        entry.Services[0].Name.Should().Be("adguard");
        entry.Services[0].IsHealthy.Should().BeTrue();
        entry.Services[1].Name.Should().Be("grafana");
        entry.Services[1].IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task CollectEventAsync_DockerThrows_RecordsErrorAndContinues()
    {
        _mockDocker.Setup(d => d.IsDockerAvailableAsync())
            .ThrowsAsync(new Exception("Connection refused"));

        var entry = await _sut.CollectEventAsync();

        entry.Should().NotBeNull();
        entry.Errors.Should().Contain(e => e.Contains("Docker"));
    }

    [Fact]
    public async Task CollectEventAsync_HealthCheckThrows_RecordsErrorAndContinues()
    {
        _mockHealthCheck.Setup(h => h.CheckAllServicesAsync())
            .ThrowsAsync(new Exception("Health check timeout"));

        var entry = await _sut.CollectEventAsync();

        entry.Should().NotBeNull();
        entry.Errors.Should().Contain(e => e.Contains("Health"));
    }

    [Fact]
    public async Task CollectEventAsync_AllSourcesFail_ReturnsEntryWithErrors()
    {
        _mockDocker.Setup(d => d.IsDockerAvailableAsync())
            .ThrowsAsync(new Exception("Docker fail"));
        _mockHealthCheck.Setup(h => h.CheckAllServicesAsync())
            .ThrowsAsync(new Exception("Health fail"));
        _mockNmap.Setup(n => n.IsNmapAvailable()).Returns(false);

        var entry = await _sut.CollectEventAsync();

        entry.Should().NotBeNull();
        entry.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CollectEventAsync_PowerSnapshot_IsNotNull()
    {
        var entry = await _sut.CollectEventAsync();

        entry.Power.Should().NotBeNull();
    }
}
