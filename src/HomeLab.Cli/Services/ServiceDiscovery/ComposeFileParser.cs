using HomeLab.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomeLab.Cli.Services.ServiceDiscovery;

/// <summary>
/// Parses docker-compose.yml files to extract service definitions.
/// </summary>
public class ComposeFileParser
{
    /// <summary>
    /// Parses a docker-compose.yml file and returns service definitions.
    /// </summary>
    public List<ServiceDefinition> Parse(string composeFilePath)
    {
        if (!File.Exists(composeFilePath))
        {
            throw new FileNotFoundException($"Docker compose file not found: {composeFilePath}");
        }

        var yaml = File.ReadAllText(composeFilePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var composeFile = deserializer.Deserialize<DockerComposeFile>(yaml);

        if (composeFile?.Services == null)
        {
            return new List<ServiceDefinition>();
        }

        var services = new List<ServiceDefinition>();

        foreach (var (name, service) in composeFile.Services)
        {
            var definition = new ServiceDefinition
            {
                Name = name,
                Image = service.Image ?? string.Empty,
                Type = ClassifyService(name, service.Image),
                Ports = service.Ports ?? new List<string>(),
                Volumes = service.Volumes ?? new List<string>(),
                Environment = ParseEnvironment(service.Environment),
                DependsOn = service.DependsOn ?? new List<string>(),
                IsEnabled = true
            };

            services.Add(definition);
        }

        return services;
    }

    /// <summary>
    /// Classifies a service based on its name and image.
    /// </summary>
    private ServiceType ClassifyService(string name, string? image)
    {
        var lowerName = name.ToLowerInvariant();
        var lowerImage = image?.ToLowerInvariant() ?? string.Empty;

        // DNS services
        if (lowerName.Contains("adguard") || lowerImage.Contains("adguard"))
        {
            return ServiceType.Dns;
        }

        if (lowerName.Contains("pihole") || lowerImage.Contains("pihole"))
        {
            return ServiceType.Dns;
        }

        // VPN services
        if (lowerName.Contains("wireguard") || lowerImage.Contains("wireguard"))
        {
            return ServiceType.Vpn;
        }

        if (lowerName.Contains("openvpn") || lowerImage.Contains("openvpn"))
        {
            return ServiceType.Vpn;
        }

        // Monitoring services
        if (lowerName.Contains("prometheus") || lowerImage.Contains("prometheus"))
        {
            return ServiceType.Monitoring;
        }

        // Dashboard services
        if (lowerName.Contains("grafana") || lowerImage.Contains("grafana"))
        {
            return ServiceType.Dashboard;
        }

        // Metrics exporters
        if (lowerName.Contains("exporter") || lowerImage.Contains("exporter"))
        {
            return ServiceType.Metrics;
        }

        // Databases
        if (lowerImage.Contains("postgres") || lowerImage.Contains("mysql") ||
            lowerImage.Contains("mongodb") || lowerImage.Contains("redis"))
        {
            return ServiceType.Database;
        }

        // Web servers
        if (lowerImage.Contains("nginx") || lowerImage.Contains("apache") ||
            lowerImage.Contains("caddy"))
        {
            return ServiceType.WebServer;
        }

        return ServiceType.Application;
    }

    /// <summary>
    /// Parses environment variables from different possible formats.
    /// </summary>
    private Dictionary<string, string> ParseEnvironment(object? environment)
    {
        if (environment == null)
        {
            return new Dictionary<string, string>();
        }

        // Environment can be either a list of strings (KEY=VALUE) or a dictionary
        if (environment is List<object> list)
        {
            var dict = new Dictionary<string, string>();
            foreach (var item in list)
            {
                var str = item.ToString();
                if (string.IsNullOrEmpty(str))
                {
                    continue;
                }

                var parts = str.Split('=', 2);
                if (parts.Length == 2)
                {
                    dict[parts[0]] = parts[1];
                }
            }
            return dict;
        }

        if (environment is Dictionary<object, object> objDict)
        {
            return objDict.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => kvp.Value?.ToString() ?? string.Empty
            );
        }

        return new Dictionary<string, string>();
    }
}

/// <summary>
/// Represents the structure of a docker-compose.yml file.
/// </summary>
internal class DockerComposeFile
{
    public Dictionary<string, ComposeService>? Services { get; set; }
}

/// <summary>
/// Represents a service in a docker-compose file.
/// </summary>
internal class ComposeService
{
    public string? Image { get; set; }
    public string? ContainerName { get; set; }
    public List<string>? Ports { get; set; }
    public List<string>? Volumes { get; set; }
    public object? Environment { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Restart { get; set; }
}
