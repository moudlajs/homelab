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

    /// <summary>
    /// Gets logs from a container.
    /// </summary>
    Task<string> GetContainerLogsAsync(string name, int tailLines = 100);

    /// <summary>
    /// Pulls the latest image for a container.
    /// </summary>
    Task PullImageAsync(string imageName);

    /// <summary>
    /// Removes unused images and containers.
    /// </summary>
    Task<CleanupResult> CleanupAsync(bool includeVolumes = false);
}

/// <summary>
/// Result of a cleanup operation.
/// </summary>
public class CleanupResult
{
    public int RemovedContainers { get; set; }
    public int RemovedImages { get; set; }
    public int RemovedVolumes { get; set; }
    public ulong SpaceReclaimed { get; set; }
}
