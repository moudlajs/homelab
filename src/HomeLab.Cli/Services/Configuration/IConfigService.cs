namespace HomeLab.Cli.Services.Configuration;

/// <summary>
/// Interface for configuration management operations.
/// Handles reading, writing, and backing up configuration files.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Gets the docker-compose file content.
    /// </summary>
    Task<string> GetComposeFileAsync();

    /// <summary>
    /// Updates the docker-compose file with new content.
    /// Automatically creates a backup before updating.
    /// </summary>
    Task UpdateComposeFileAsync(string content);

    /// <summary>
    /// Creates a timestamped backup of the current configuration.
    /// </summary>
    Task<string> BackupConfigAsync();

    /// <summary>
    /// Lists all available configuration backups.
    /// </summary>
    Task<List<string>> ListBackupsAsync();

    /// <summary>
    /// Restores a configuration from a backup file.
    /// </summary>
    Task RestoreBackupAsync(string backupName);
}
