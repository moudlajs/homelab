using HomeLab.Cli.Models;
using Renci.SshNet;

namespace HomeLab.Cli.Services.Remote;

/// <summary>
/// SSH service implementation using SSH.NET library.
/// </summary>
public class SshService : ISshService
{
    public async Task<bool> TestConnectionAsync(RemoteConnection connection)
    {
        try
        {
            using var client = CreateSshClient(connection);
            await Task.Run(() => client.Connect());
            var isConnected = client.IsConnected;
            client.Disconnect();
            return isConnected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<CommandResult> ExecuteCommandAsync(RemoteConnection connection, string command)
    {
        using var client = CreateSshClient(connection);

        await Task.Run(() => client.Connect());

        if (!client.IsConnected)
        {
            return new CommandResult
            {
                ExitCode = -1,
                Error = "Failed to connect to remote host"
            };
        }

        try
        {
            var cmd = client.CreateCommand(command);
            var result = await Task.Run(() => cmd.Execute());

            return new CommandResult
            {
                ExitCode = cmd.ExitStatus ?? -1,
                Output = result,
                Error = cmd.Error
            };
        }
        finally
        {
            client.Disconnect();
        }
    }

    public async Task UploadFileAsync(RemoteConnection connection, string localPath, string remotePath)
    {
        using var client = CreateSftpClient(connection);

        await Task.Run(() => client.Connect());

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Failed to connect to remote host");
        }

        try
        {
            using var fileStream = File.OpenRead(localPath);
            await Task.Run(() => client.UploadFile(fileStream, remotePath, true));
        }
        finally
        {
            client.Disconnect();
        }
    }

    public async Task DownloadFileAsync(RemoteConnection connection, string remotePath, string localPath)
    {
        using var client = CreateSftpClient(connection);

        await Task.Run(() => client.Connect());

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Failed to connect to remote host");
        }

        try
        {
            using var fileStream = File.Create(localPath);
            await Task.Run(() => client.DownloadFile(remotePath, fileStream));
        }
        finally
        {
            client.Disconnect();
        }
    }

    public async Task<bool> IsDockerRunningAsync(RemoteConnection connection)
    {
        var result = await ExecuteCommandAsync(connection, "docker info >/dev/null 2>&1 && echo 'running' || echo 'not running'");
        return result.Success && result.Output.Contains("running");
    }

    private SshClient CreateSshClient(RemoteConnection connection)
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrEmpty(connection.KeyFile))
        {
            // Use SSH key authentication
            var keyFile = new PrivateKeyFile(ExpandPath(connection.KeyFile));
            var keyAuth = new PrivateKeyAuthenticationMethod(connection.Username, keyFile);
            connectionInfo = new ConnectionInfo(connection.Host, connection.Port, connection.Username, keyAuth);
        }
        else
        {
            // Use password authentication (will prompt if needed)
            // For now, we'll use keyboard-interactive which can handle various auth methods
            var keyboardAuth = new KeyboardInteractiveAuthenticationMethod(connection.Username);
            connectionInfo = new ConnectionInfo(connection.Host, connection.Port, connection.Username, keyboardAuth);
        }

        return new SshClient(connectionInfo);
    }

    private SftpClient CreateSftpClient(RemoteConnection connection)
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrEmpty(connection.KeyFile))
        {
            var keyFile = new PrivateKeyFile(ExpandPath(connection.KeyFile));
            var keyAuth = new PrivateKeyAuthenticationMethod(connection.Username, keyFile);
            connectionInfo = new ConnectionInfo(connection.Host, connection.Port, connection.Username, keyAuth);
        }
        else
        {
            var keyboardAuth = new KeyboardInteractiveAuthenticationMethod(connection.Username);
            connectionInfo = new ConnectionInfo(connection.Host, connection.Port, connection.Username, keyboardAuth);
        }

        return new SftpClient(connectionInfo);
    }

    private string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }
        return path;
    }
}
