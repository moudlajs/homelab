namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a remote homelab connection profile.
/// </summary>
public class RemoteConnection
{
    /// <summary>
    /// Unique name for this connection (e.g., "mac-mini", "production").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Remote host (IP address or hostname).
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SSH port (default: 22).
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// SSH username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Path to SSH private key file (optional if using password).
    /// </summary>
    public string? KeyFile { get; set; }

    /// <summary>
    /// Docker socket path on remote host.
    /// </summary>
    public string DockerSocket { get; set; } = "unix:///var/run/docker.sock";

    /// <summary>
    /// Path to docker-compose.yml on remote host.
    /// </summary>
    public string? ComposeFilePath { get; set; }

    /// <summary>
    /// Whether this is the default connection.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Last successful connection timestamp.
    /// </summary>
    public DateTime? LastConnected { get; set; }

    /// <summary>
    /// Optional description/notes.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Collection of remote connection profiles.
/// </summary>
public class RemoteConnectionProfiles
{
    /// <summary>
    /// List of all configured remote connections.
    /// </summary>
    public List<RemoteConnection> Connections { get; set; } = new();

    /// <summary>
    /// Gets the default connection if one is set.
    /// </summary>
    public RemoteConnection? GetDefaultConnection()
    {
        return Connections.FirstOrDefault(c => c.IsDefault);
    }

    /// <summary>
    /// Gets a connection by name.
    /// </summary>
    public RemoteConnection? GetConnection(string name)
    {
        return Connections.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or updates a connection.
    /// </summary>
    public void AddOrUpdateConnection(RemoteConnection connection)
    {
        var existing = GetConnection(connection.Name);
        if (existing != null)
        {
            Connections.Remove(existing);
        }

        Connections.Add(connection);
    }

    /// <summary>
    /// Removes a connection by name.
    /// </summary>
    public bool RemoveConnection(string name)
    {
        var connection = GetConnection(name);
        if (connection != null)
        {
            Connections.Remove(connection);
            return true;
        }
        return false;
    }
}
