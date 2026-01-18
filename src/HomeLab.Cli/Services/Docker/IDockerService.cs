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

    /// <summary>
    /// Gets Docker system information.
    /// </summary>
    Task<SystemInfo> GetSystemInfoAsync();

    /// <summary>
    /// Executes a command inside a running container.
    /// </summary>
    /// <param name="containerName">Container name or ID</param>
    /// <param name="command">Command and arguments to execute</param>
    /// <returns>Command output (stdout)</returns>
    Task<string> ExecInContainerAsync(string containerName, params string[] command);

    /// <summary>
    /// Checks if a container exists (regardless of running state).
    /// </summary>
    Task<bool> ContainerExistsAsync(string containerName);

    /// <summary>
    /// Checks if a container is running.
    /// </summary>
    Task<bool> IsContainerRunningAsync(string containerName);
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

/// <summary>
/// Docker system information.
/// </summary>
public class SystemInfo
{
    public string? ServerVersion { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Architecture { get; set; }
    public long NCPU { get; set; }
    public long MemTotal { get; set; }
    public long Containers { get; set; }
    public long ContainersRunning { get; set; }
    public long ContainersStopped { get; set; }
    public long Images { get; set; }
}
