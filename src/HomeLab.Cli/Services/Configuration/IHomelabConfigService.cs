namespace HomeLab.Cli.Services.Configuration;

/// <summary>
/// Interface for homelab configuration management.
/// Reads settings from config/homelab-cli.yaml.
/// </summary>
public interface IHomelabConfigService
{
    /// <summary>
    /// Loads the configuration from file.
    /// </summary>
    Task<HomelabConfig> LoadConfigAsync();

    /// <summary>
    /// Gets the Docker host connection string.
    /// </summary>
    string DockerHost { get; }

    /// <summary>
    /// Gets the path to the docker-compose file.
    /// </summary>
    string ComposeFilePath { get; }

    /// <summary>
    /// Gets service-specific configuration.
    /// </summary>
    ServiceConfig GetServiceConfig(string serviceName);

    /// <summary>
    /// Gets Home Assistant configuration (URL defaults to http://localhost:8123).
    /// </summary>
    ServiceConfig GetHomeAssistantConfig();

    /// <summary>
    /// Gets GitHub personal access token for self-update (required for private repos).
    /// </summary>
    string? GetGitHubToken();
}

/// <summary>
/// Complete homelab configuration.
/// </summary>
public class HomelabConfig
{
    public DevelopmentConfig Development { get; set; } = new();
    public Dictionary<string, ServiceConfig> Services { get; set; } = new();
    public RemoteConfig Remote { get; set; } = new();
    public GitHubConfig? Github { get; set; }
}

/// <summary>
/// Development environment settings.
/// </summary>
public class DevelopmentConfig
{
    public string DockerHost { get; set; } = "unix:///var/run/docker.sock";
    public string ComposeFile { get; set; } = "~/Projects/homelab-mock/docker-compose.yml";
}

/// <summary>
/// Service-specific configuration.
/// </summary>
public class ServiceConfig
{
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public string? ConfigPath { get; set; }
    public string? LogPath { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Remote server configuration.
/// </summary>
public class RemoteConfig
{
    public RemoteHostConfig MacMini { get; set; } = new();
}

/// <summary>
/// Remote host configuration.
/// </summary>
public class RemoteHostConfig
{
    public string Host { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string DockerHost { get; set; } = "unix:///var/run/docker.sock";
    public string ComposeFile { get; set; } = string.Empty;
}

/// <summary>
/// GitHub configuration for self-update.
/// </summary>
public class GitHubConfig
{
    public string? Token { get; set; }
}
