using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Docker;

/// <summary>
/// Interface for Docker operations.
/// Using an interface allows us to mock this in tests.
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Lists all containers, optionally filtered to homelab namespace.
    /// </summary>
    Task<List<ContainerInfo>> ListContainersAsync(bool onlyHomelab = true);

    /// <summary>
    /// Starts a container by name.
    /// </summary>
    Task StartContainerAsync(string name);

    /// <summary>
    /// Stops a container by name.
    /// </summary>
    Task StopContainerAsync(string name);
}
