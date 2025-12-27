using HomeLab.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomeLab.Cli.Services.Remote;

/// <summary>
/// Service for managing remote connection profiles.
/// Stores connections in ~/.homelab/remotes.yaml
/// </summary>
public class RemoteConnectionService
{
    private readonly string _profilesPath;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public RemoteConnectionService()
    {
        var homelabDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".homelab");

        Directory.CreateDirectory(homelabDir);
        _profilesPath = Path.Combine(homelabDir, "remotes.yaml");

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Loads all remote connection profiles.
    /// </summary>
    public RemoteConnectionProfiles LoadProfiles()
    {
        if (!File.Exists(_profilesPath))
        {
            return new RemoteConnectionProfiles();
        }

        try
        {
            var yaml = File.ReadAllText(_profilesPath);
            var profiles = _deserializer.Deserialize<RemoteConnectionProfiles>(yaml);
            return profiles ?? new RemoteConnectionProfiles();
        }
        catch
        {
            return new RemoteConnectionProfiles();
        }
    }

    /// <summary>
    /// Saves remote connection profiles.
    /// </summary>
    public void SaveProfiles(RemoteConnectionProfiles profiles)
    {
        var yaml = _serializer.Serialize(profiles);
        File.WriteAllText(_profilesPath, yaml);
    }

    /// <summary>
    /// Adds a new connection profile.
    /// </summary>
    public void AddConnection(RemoteConnection connection)
    {
        var profiles = LoadProfiles();
        profiles.AddOrUpdateConnection(connection);
        SaveProfiles(profiles);
    }

    /// <summary>
    /// Removes a connection profile by name.
    /// </summary>
    public bool RemoveConnection(string name)
    {
        var profiles = LoadProfiles();
        var removed = profiles.RemoveConnection(name);
        if (removed)
        {
            SaveProfiles(profiles);
        }
        return removed;
    }

    /// <summary>
    /// Gets a connection by name.
    /// </summary>
    public RemoteConnection? GetConnection(string name)
    {
        var profiles = LoadProfiles();
        return profiles.GetConnection(name);
    }

    /// <summary>
    /// Gets the default connection.
    /// </summary>
    public RemoteConnection? GetDefaultConnection()
    {
        var profiles = LoadProfiles();
        return profiles.GetDefaultConnection();
    }

    /// <summary>
    /// Sets a connection as the default.
    /// </summary>
    public void SetDefaultConnection(string name)
    {
        var profiles = LoadProfiles();

        // Clear existing default
        foreach (var conn in profiles.Connections)
        {
            conn.IsDefault = false;
        }

        // Set new default
        var connection = profiles.GetConnection(name);
        if (connection != null)
        {
            connection.IsDefault = true;
            SaveProfiles(profiles);
        }
    }

    /// <summary>
    /// Lists all connection profiles.
    /// </summary>
    public List<RemoteConnection> ListConnections()
    {
        var profiles = LoadProfiles();
        return profiles.Connections;
    }
}
