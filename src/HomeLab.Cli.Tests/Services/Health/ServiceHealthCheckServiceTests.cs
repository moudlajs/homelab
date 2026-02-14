using FluentAssertions;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.ServiceDiscovery;
using Moq;
using Xunit;

namespace HomeLab.Cli.Tests.Services.Health;

public class ServiceHealthCheckServiceTests
{
    private readonly Mock<IServiceDiscoveryService> _mockDiscovery;
    private readonly Mock<IDockerService> _mockDocker;
    private readonly Mock<IServiceClientFactory> _mockClientFactory;
    private readonly ServiceHealthCheckService _sut;

    public ServiceHealthCheckServiceTests()
    {
        _mockDiscovery = new Mock<IServiceDiscoveryService>();
        _mockDocker = new Mock<IDockerService>();
        _mockClientFactory = new Mock<IServiceClientFactory>();

        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>());
        _mockDiscovery.Setup(d => d.DiscoverServicesAsync())
            .ReturnsAsync(new List<ServiceDefinition>());

        _sut = new ServiceHealthCheckService(
            _mockDiscovery.Object,
            _mockDocker.Object,
            _mockClientFactory.Object);
    }

    [Fact]
    public async Task CheckAllServicesAsync_NoServices_ReturnsEmptyList()
    {
        var results = await _sut.CheckAllServicesAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAllServicesAsync_ReturnsResultPerService()
    {
        _mockDiscovery.Setup(d => d.DiscoverServicesAsync())
            .ReturnsAsync(new List<ServiceDefinition>
            {
                new() { Name = "adguard", Type = ServiceType.Dns },
                new() { Name = "grafana", Type = ServiceType.Dashboard }
            });

        var results = await _sut.CheckAllServicesAsync();

        results.Should().HaveCount(2);
        results.Select(r => r.ServiceName).Should().Contain("adguard", "grafana");
    }

    [Fact]
    public async Task CheckServiceAsync_ContainerRunning_SetsIsRunningTrue()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_adguard", IsRunning = true }
            });

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };

        // Mock the AdGuard client for service-specific health check
        var mockClient = new Mock<IAdGuardClient>();
        mockClient.Setup(c => c.GetHealthInfoAsync())
            .ReturnsAsync(new ServiceHealthInfo { IsHealthy = true, ServiceName = "adguard" });
        _mockClientFactory.Setup(f => f.CreateAdGuardClient()).Returns(mockClient.Object);

        var result = await _sut.CheckServiceAsync(service);

        result.IsRunning.Should().BeTrue();
        result.Status.Should().Be("running");
    }

    [Fact]
    public async Task CheckServiceAsync_ContainerStopped_SetsIsRunningFalse()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_adguard", IsRunning = false }
            });

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };
        var result = await _sut.CheckServiceAsync(service);

        result.IsRunning.Should().BeFalse();
        result.Status.Should().Be("stopped");
        result.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task CheckServiceAsync_ContainerNotFound_SetsNotFoundStatus()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>());

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };
        var result = await _sut.CheckServiceAsync(service);

        result.IsRunning.Should().BeFalse();
        result.Status.Should().Be("not found");
    }

    [Fact]
    public async Task CheckServiceAsync_DockerThrows_SetsErrorStatus()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ThrowsAsync(new Exception("Docker daemon not running"));

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };
        var result = await _sut.CheckServiceAsync(service);

        result.IsRunning.Should().BeFalse();
        result.Status.Should().Be("error");
        result.Message.Should().Contain("Docker check failed");
    }

    [Fact]
    public async Task CheckServiceAsync_RunningWithHealthCheck_SetsHealthInfo()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_adguard", IsRunning = true }
            });

        var mockClient = new Mock<IAdGuardClient>();
        mockClient.Setup(c => c.GetHealthInfoAsync())
            .ReturnsAsync(new ServiceHealthInfo
            {
                IsHealthy = true,
                ServiceName = "adguard",
                Metrics = new Dictionary<string, string> { ["queries"] = "1000" }
            });
        _mockClientFactory.Setup(f => f.CreateAdGuardClient()).Returns(mockClient.Object);

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };
        var result = await _sut.CheckServiceAsync(service);

        result.IsHealthy.Should().BeTrue();
        result.Metrics.Should().ContainKey("queries");
    }

    [Fact]
    public async Task CheckServiceAsync_HealthCheckFails_SetsHealthyFalse()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_adguard", IsRunning = true }
            });

        var mockClient = new Mock<IAdGuardClient>();
        mockClient.Setup(c => c.GetHealthInfoAsync())
            .ThrowsAsync(new Exception("Connection refused"));
        _mockClientFactory.Setup(f => f.CreateAdGuardClient()).Returns(mockClient.Object);

        var service = new ServiceDefinition { Name = "adguard", Type = ServiceType.Dns };
        var result = await _sut.CheckServiceAsync(service);

        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Health check failed");
    }

    [Fact]
    public async Task CheckServiceAsync_UnknownServiceType_SkipsSpecificHealthCheck()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_myapp", IsRunning = true }
            });

        var service = new ServiceDefinition { Name = "myapp", Type = ServiceType.Application };
        var result = await _sut.CheckServiceAsync(service);

        result.IsRunning.Should().BeTrue();
        result.IsHealthy.Should().BeFalse();
        result.ServiceHealth.Should().BeNull();
    }

    [Fact]
    public async Task CheckServiceAsync_VpnService_UsesTailscaleClient()
    {
        _mockDocker.Setup(d => d.ListContainersAsync(true))
            .ReturnsAsync(new List<ContainerInfo>
            {
                new() { Name = "homelab_tailscale", IsRunning = true }
            });

        var mockClient = new Mock<ITailscaleClient>();
        mockClient.Setup(c => c.GetHealthInfoAsync())
            .ReturnsAsync(new ServiceHealthInfo { IsHealthy = true, ServiceName = "tailscale" });
        _mockClientFactory.Setup(f => f.CreateTailscaleClient()).Returns(mockClient.Object);

        var service = new ServiceDefinition { Name = "tailscale", Type = ServiceType.Vpn };
        var result = await _sut.CheckServiceAsync(service);

        result.IsHealthy.Should().BeTrue();
        _mockClientFactory.Verify(f => f.CreateTailscaleClient(), Times.Once);
    }
}
