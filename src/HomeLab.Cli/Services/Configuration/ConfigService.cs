namespace HomeLab.Cli.Services.Configuration;

/// <summary>
/// Implementation of IConfigService for managing docker-compose configurations.
/// Handles file operations, backups, and validation.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly string _configPath;
    private readonly string _backupDirectory;

    public ConfigService()
    {
        // Default paths - can be made configurable later
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "homelab", "docker-compose.yml");
        _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "homelab", "backups");

        // Ensure backup directory exists
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task<string> GetComposeFileAsync()
    {
        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException(
                $"Docker compose file not found at {_configPath}. " +
                "Please ensure your homelab configuration exists.");
        }

        return await File.ReadAllTextAsync(_configPath);
    }

    public async Task UpdateComposeFileAsync(string content)
    {
        // Create backup before updating
        await BackupConfigAsync();

        // Write new content
        await File.WriteAllTextAsync(_configPath, content);
    }

    public async Task<string> BackupConfigAsync()
    {
        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException($"Configuration file not found at {_configPath}");
        }

        // Create timestamped backup filename
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"docker-compose.{timestamp}.yml.bak";
        var backupPath = Path.Combine(_backupDirectory, backupFileName);

        // Copy config to backup
        await File.WriteAllTextAsync(backupPath, await File.ReadAllTextAsync(_configPath));

        return backupFileName;
    }

    public async Task<List<string>> ListBackupsAsync()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return new List<string>();
        }

        var backups = Directory.GetFiles(_backupDirectory, "*.yml.bak")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderByDescending(name => name)
            .ToList();

        return await Task.FromResult(backups);
    }

    public async Task RestoreBackupAsync(string backupName)
    {
        var backupPath = Path.Combine(_backupDirectory, backupName);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup '{backupName}' not found");
        }

        // Create a backup of current config before restoring
        if (File.Exists(_configPath))
        {
            await BackupConfigAsync();
        }

        // Restore from backup
        var backupContent = await File.ReadAllTextAsync(backupPath);
        await File.WriteAllTextAsync(_configPath, backupContent);
    }
}
