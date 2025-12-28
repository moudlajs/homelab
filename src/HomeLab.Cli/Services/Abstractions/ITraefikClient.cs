using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for Traefik reverse proxy operations.
/// </summary>
public interface ITraefikClient : IServiceClient
{
    /// <summary>
    /// Gets all HTTP routers (routes).
    /// </summary>
    Task<List<TraefikRoute>> GetRoutesAsync();

    /// <summary>
    /// Gets all backend services.
    /// </summary>
    Task<List<TraefikService>> GetServicesAsync();

    /// <summary>
    /// Gets all middlewares.
    /// </summary>
    Task<List<TraefikMiddleware>> GetMiddlewaresAsync();

    /// <summary>
    /// Gets Traefik overview/status.
    /// </summary>
    Task<TraefikOverview> GetOverviewAsync();
}
