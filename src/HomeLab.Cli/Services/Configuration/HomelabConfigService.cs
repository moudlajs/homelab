using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomeLab.Cli.Services.Configuration;

/// <summary>
/// Service for loading and managing homelab configuration.
/// </summary>
public class HomelabConfigService : IHomelabConfigService
{
    private readonly string _configPath;
    private HomelabConfig? _config;

    public HomelabConfigService()
    {
        // Config file location: Try multiple locations in priority order
        // 1. ~/.config/homelab/homelab-cli.yaml (standard Linux/Mac location)
        // 2. ./config/homelab-cli.yaml (repo root, for development)

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userConfigPath = Path.Combine(home, ".config", "homelab", "homelab-cli.yaml");
        var repoConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "homelab-cli.yaml");

        // Use user config if it exists, otherwise fall back to repo config
        _configPath = File.Exists(userConfigPath) ? userConfigPath : repoConfigPath;
    }

    public async Task<HomelabConfig> LoadConfigAsync()
    {
        if (_config != null)
        {
            return _config;
        }

        if (!File.Exists(_configPath))
        {
            // Return default config if file doesn't exist
            _config = new HomelabConfig();
            return _config;
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(_configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            _config = deserializer.Deserialize<HomelabConfig>(yaml) ?? new HomelabConfig();
            return _config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load config from {_configPath}: {ex.Message}", ex);
        }
    }

    public bool UseMockServices
    {
        get
        {
            _config ??= LoadConfigAsync().GetAwaiter().GetResult();
            return _config.Development.UseMockServices;
        }
    }

    public string DockerHost
    {
        get
        {
            _config ??= LoadConfigAsync().GetAwaiter().GetResult();
            return _config.Development.DockerHost;
        }
    }

    public string ComposeFilePath
    {
        get
        {
            _config ??= LoadConfigAsync().GetAwaiter().GetResult();
            var path = _config.Development.ComposeFile;

            // Expand ~ to home directory
            if (path.StartsWith("~"))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, path[2..]);
            }

            return path;
        }
    }

    public ServiceConfig GetServiceConfig(string serviceName)
    {
        _config ??= LoadConfigAsync().GetAwaiter().GetResult();

        if (_config.Services.TryGetValue(serviceName.ToLowerInvariant(), out var config))
        {
            return config;
        }

        // Return default config if service not found
        return new ServiceConfig { Enabled = false };
    }

    public ServiceConfig GetHomeAssistantConfig()
    {
        var config = GetServiceConfig("homeassistant");

        // Default URL if not configured
        if (string.IsNullOrEmpty(config.Url))
        {
            config.Url = "http://localhost:8123";
        }

        return config;
    }

    public string? GetGitHubToken()
    {
        _config ??= LoadConfigAsync().GetAwaiter().GetResult();
        return _config.Github?.Token;
    }
}
