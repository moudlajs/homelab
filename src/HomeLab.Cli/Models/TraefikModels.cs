namespace HomeLab.Cli.Models;

/// <summary>
/// Traefik HTTP router (route) information.
/// </summary>
public class TraefikRoute
{
    public string Name { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Middlewares { get; set; } = new();
    public bool TLS { get; set; }
}

/// <summary>
/// Traefik backend service information.
/// </summary>
public class TraefikService
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Servers { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string LoadBalancer { get; set; } = string.Empty;
}

/// <summary>
/// Traefik middleware information.
/// </summary>
public class TraefikMiddleware
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Traefik overview/status information.
/// </summary>
public class TraefikOverview
{
    public int TotalRouters { get; set; }
    public int TotalServices { get; set; }
    public int TotalMiddlewares { get; set; }
    public int HealthyRouters { get; set; }
    public int HealthyServices { get; set; }
    public List<string> EntryPoints { get; set; } = new();
}
