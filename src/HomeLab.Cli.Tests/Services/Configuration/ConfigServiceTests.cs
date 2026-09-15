using FluentAssertions;
using HomeLab.Cli.Services.Configuration;
using Moq;
using Xunit;

namespace HomeLab.Cli.Tests.Services.Configuration;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _composePath;
    private readonly Mock<IHomelabConfigService> _homelabConfig = new();
    private readonly ConfigService _sut;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"homelab-configsvc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _composePath = Path.Combine(_testDir, "docker-compose.yml");

        _homelabConfig.SetupGet(x => x.ComposeFilePath).Returns(_composePath);
        _sut = new ConfigService(_homelabConfig.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetComposeFileAsync_ReadsFromConfiguredPath()
    {
        await File.WriteAllTextAsync(_composePath, "services: {}");

        var content = await _sut.GetComposeFileAsync();

        content.Should().Be("services: {}");
    }

    [Fact]
    public async Task GetComposeFileAsync_MissingFile_ThrowsNamingConfiguredPath()
    {
        var act = async () => await _sut.GetComposeFileAsync();

        var ex = await act.Should().ThrowAsync<FileNotFoundException>();
        ex.Which.Message.Should().Contain(_composePath);
    }

    [Fact]
    public async Task BackupConfigAsync_WritesBackupBesideComposeFile()
    {
        await File.WriteAllTextAsync(_composePath, "services: {}");

        var backupName = await _sut.BackupConfigAsync();

        var backupPath = Path.Combine(_testDir, "backups", backupName);
        File.Exists(backupPath).Should().BeTrue();
        (await File.ReadAllTextAsync(backupPath)).Should().Be("services: {}");
    }

    [Fact]
    public async Task ListBackupsAsync_NoBackupDirectory_ReturnsEmpty()
    {
        var backups = await _sut.ListBackupsAsync();

        backups.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackupsAsync_AfterBackup_ReturnsIt()
    {
        await File.WriteAllTextAsync(_composePath, "services: {}");
        var backupName = await _sut.BackupConfigAsync();

        var backups = await _sut.ListBackupsAsync();

        backups.Should().Contain(backupName);
    }

    [Fact]
    public async Task ConfigService_DoesNotCreateDirectoriesUntilBackupRequested()
    {
        // Constructing the service must not have side effects on disk.
        Directory.Exists(Path.Combine(_testDir, "backups")).Should().BeFalse();

        await File.WriteAllTextAsync(_composePath, "services: {}");
        await _sut.BackupConfigAsync();

        Directory.Exists(Path.Combine(_testDir, "backups")).Should().BeTrue();
    }
}
