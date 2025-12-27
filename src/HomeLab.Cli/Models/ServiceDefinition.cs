namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a service defined in docker-compose.
/// Parsed from docker-compose.yml during service discovery.
/// </summary>
public class ServiceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public ServiceType Type { get; set; } = ServiceType.Unknown;
    public List<string> Ports { get; set; } = new();
    public List<string> Volumes { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Type of homelab service.
/// </summary>
public enum ServiceType
{
    Unknown,
    Dns,        // AdGuard Home
    Vpn,        // WireGuard
    Monitoring, // Prometheus
    Dashboard,  // Grafana
    Metrics,    // Node Exporter
    Database,   // PostgreSQL, MySQL, etc.
    WebServer,  // Nginx, Apache, etc.
    Application // Custom applications
}
