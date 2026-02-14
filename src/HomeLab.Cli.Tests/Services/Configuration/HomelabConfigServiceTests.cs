using FluentAssertions;
using HomeLab.Cli.Services.Configuration;
using Xunit;

namespace HomeLab.Cli.Tests.Services.Configuration;

public class HomelabConfigServiceTests : IDisposable
{
    private readonly string _testDir;

    public HomelabConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"homelab-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private string CreateConfigFile(string yaml)
    {
        var configPath = Path.Combine(_testDir, "homelab-cli.yaml");
        File.WriteAllText(configPath, yaml);
        return configPath;
    }

    [Fact]
    public async Task LoadConfigAsync_MissingFile_ReturnsDefaults()
    {
        var sut = new HomelabConfigService();
        var config = await sut.LoadConfigAsync();

        config.Should().NotBeNull();
        config.Development.Should().NotBeNull();
        config.Services.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadConfigAsync_CachesResult()
    {
        var sut = new HomelabConfigService();
        var first = await sut.LoadConfigAsync();
        var second = await sut.LoadConfigAsync();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void GetServiceConfig_UnknownService_ReturnsDisabledDefault()
    {
        var sut = new HomelabConfigService();
        var config = sut.GetServiceConfig("nonexistent");

        config.Should().NotBeNull();
        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void GetServiceConfig_IsCaseInsensitive()
    {
        var sut = new HomelabConfigService();
        var lower = sut.GetServiceConfig("adguard");
        var upper = sut.GetServiceConfig("ADGUARD");

        lower.Enabled.Should().Be(upper.Enabled);
    }

    [Fact]
    public void GetHomeAssistantConfig_NoConfig_ReturnsDefaultUrl()
    {
        var sut = new HomelabConfigService();
        var config = sut.GetHomeAssistantConfig();

        config.Should().NotBeNull();
        config.Url.Should().Be("http://localhost:8123");
    }

    [Fact]
    public void GetGitHubToken_NoConfig_ReturnsNull()
    {
        var sut = new HomelabConfigService();
        var token = sut.GetGitHubToken();

        token.Should().BeNull();
    }

    [Fact]
    public void DockerHost_Default_ReturnsUnixSocket()
    {
        var sut = new HomelabConfigService();
        sut.DockerHost.Should().Be("unix:///var/run/docker.sock");
    }

    [Fact]
    public void ComposeFilePath_Default_ContainsDockerCompose()
    {
        var sut = new HomelabConfigService();
        sut.ComposeFilePath.Should().Contain("docker-compose.yml");
    }

    [Fact]
    public void ComposeFilePath_TildeExpansion_ExpandsToHomedir()
    {
        var sut = new HomelabConfigService();
        var path = sut.ComposeFilePath;

        path.Should().NotStartWith("~");
        path.Should().StartWith("/");
    }
}
