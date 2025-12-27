using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Remote;

/// <summary>
/// Service for executing commands on remote hosts via SSH.
/// </summary>
public interface ISshService
{
    /// <summary>
    /// Tests if a connection to the remote host is successful.
    /// </summary>
    Task<bool> TestConnectionAsync(RemoteConnection connection);

    /// <summary>
    /// Executes a command on the remote host and returns the output.
    /// </summary>
    Task<CommandResult> ExecuteCommandAsync(RemoteConnection connection, string command);

    /// <summary>
    /// Uploads a file to the remote host.
    /// </summary>
    Task UploadFileAsync(RemoteConnection connection, string localPath, string remotePath);

    /// <summary>
    /// Downloads a file from the remote host.
    /// </summary>
    Task DownloadFileAsync(RemoteConnection connection, string remotePath, string localPath);

    /// <summary>
    /// Checks if Docker is running on the remote host.
    /// </summary>
    Task<bool> IsDockerRunningAsync(RemoteConnection connection);
}

/// <summary>
/// Result of executing a remote command.
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Exit code of the command (0 = success).
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Standard output from the command.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Standard error from the command.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Whether the command succeeded (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;
}
