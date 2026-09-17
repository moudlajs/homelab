namespace HomeLab.Cli.Services.Configuration;

/// <summary>
/// Implementation of IConfigService for managing docker-compose configurations.
/// Handles file operations, backups, and validation.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly IHomelabConfigService _homelabConfig;

    public ConfigService(IHomelabConfigService homelabConfig) => _homelabConfig = homelabConfig;

    /// <summary>
    /// Path to the docker-compose file, read from user configuration
    /// (development.compose_file) rather than assumed to sit under ~/homelab.
    /// </summary>
    private string ConfigPath => _homelabConfig.ComposeFilePath;

    /// <summary>
    /// Backups are kept beside the compose file they belong to.
    /// </summary>
    private string BackupDirectory =>
        Path.Combine(Path.GetDirectoryName(ConfigPath) ?? ".", "backups");

    public async Task<string> GetComposeFileAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException(
                $"Docker compose file not found at {ConfigPath}. " +
                "Please ensure your homelab configuration exists.");
        }

        return await File.ReadAllTextAsync(ConfigPath);
    }

    public async Task UpdateComposeFileAsync(string content)
    {
        // Create backup before updating
        await BackupConfigAsync();

        // Write new content
        await File.WriteAllTextAsync(ConfigPath, content);
    }

    public async Task<string> BackupConfigAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException($"Configuration file not found at {ConfigPath}");
        }

        Directory.CreateDirectory(BackupDirectory);

        // Create timestamped backup filename
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"docker-compose.{timestamp}.yml.bak";
        var backupPath = Path.Combine(BackupDirectory, backupFileName);

        // Copy config to backup
        await File.WriteAllTextAsync(backupPath, await File.ReadAllTextAsync(ConfigPath));

        return backupFileName;
    }

    public async Task<List<string>> ListBackupsAsync()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return new List<string>();
        }

        var backups = Directory.GetFiles(BackupDirectory, "*.yml.bak")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderByDescending(name => name)
            .ToList();

        return await Task.FromResult(backups);
    }

    public async Task RestoreBackupAsync(string backupName)
    {
        var backupPath = Path.Combine(BackupDirectory, backupName);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup '{backupName}' not found");
        }

        // Create a backup of current config before restoring
        if (File.Exists(ConfigPath))
        {
            await BackupConfigAsync();
        }

        // Restore from backup
        var backupContent = await File.ReadAllTextAsync(backupPath);
        await File.WriteAllTextAsync(ConfigPath, backupContent);
    }
}
