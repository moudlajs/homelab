using Docker.DotNet;
using Docker.DotNet.Models;
using HomeLab.Cli.Models;
using System.Text;

namespace HomeLab.Cli.Services.Docker;

/// <summary>
/// Implementation of IDockerService using Docker.DotNet SDK.
/// This is where the actual Docker API calls happen.
/// </summary>
public class DockerService : IDockerService
{
    private readonly DockerClient _client;

    public DockerService()
    {
        // Create Docker client
        // On macOS, Docker socket is at unix:///var/run/docker.sock
        _client = new DockerClientConfiguration(
                new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    public async Task<List<ContainerInfo>> ListContainersAsync(bool onlyHomelab = true)
    {
        // Call Docker API to list containers
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true  // Include stopped containers
            });

        // Filter to homelab_ prefix if requested
        var filtered = onlyHomelab
            ? containers.Where(c => c.Names.Any(n => n.Contains("homelab_")))
            : containers;

        // Convert Docker's data to our simple model
        return filtered.Select(c => new ContainerInfo
        {
            Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
            Id = c.ID,
            IsRunning = c.State == "running",
            Uptime = CalculateUptime(c.Created),
            // CPU/Memory require stats API (added later)
        }).ToList();
    }

    public async Task StartContainerAsync(string name)
    {
        // Find container by name
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });

        var container = containers.FirstOrDefault(c =>
            c.Names.Any(n => n.Contains(name)));

        if (container == null)
            throw new Exception($"Container '{name}' not found");

        // Start it
        await _client.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters());
    }

    public async Task StopContainerAsync(string name)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });

        var container = containers.FirstOrDefault(c =>
            c.Names.Any(n => n.Contains(name)));

        if (container == null)
            throw new Exception($"Container '{name}' not found");

        await _client.Containers.StopContainerAsync(
            container.ID,
            new ContainerStopParameters());
    }

    public async Task<string> GetContainerLogsAsync(string name, int tailLines = 100)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });

        var container = containers.FirstOrDefault(c =>
            c.Names.Any(n => n.Contains(name)));

        if (container == null)
            throw new Exception($"Container '{name}' not found");

        var logsStream = await _client.Containers.GetContainerLogsAsync(
            container.ID,
            false,
            new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tailLines.ToString()
            });

        // Read the multiplexed stream using ReadOutputAsync
        var result = new StringBuilder();
        var buffer = new byte[4096];

        var (stdout, stderr) = await logsStream.ReadOutputToEndAsync(CancellationToken.None);

        if (!string.IsNullOrEmpty(stdout))
            result.Append(stdout);
        if (!string.IsNullOrEmpty(stderr))
            result.Append(stderr);

        return result.ToString();
    }

    public async Task PullImageAsync(string imageName)
    {
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = imageName,
                Tag = "latest"
            },
            null,
            new Progress<JSONMessage>());
    }

    public async Task<CleanupResult> CleanupAsync(bool includeVolumes = false)
    {
        var result = new CleanupResult();

        // Prune stopped containers
        var containerPrune = await _client.Containers.PruneContainersAsync();
        result.RemovedContainers = containerPrune.ContainersDeleted?.Count ?? 0;
        result.SpaceReclaimed = containerPrune.SpaceReclaimed;

        // Prune dangling images
        var imagePrune = await _client.Images.PruneImagesAsync(new ImagesPruneParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["dangling"] = new Dictionary<string, bool> { ["true"] = true }
            }
        });
        result.RemovedImages = imagePrune.ImagesDeleted?.Count ?? 0;
        result.SpaceReclaimed += imagePrune.SpaceReclaimed;

        // Prune volumes if requested
        if (includeVolumes)
        {
            var volumePrune = await _client.Volumes.PruneAsync();
            result.RemovedVolumes = volumePrune.VolumesDeleted?.Count ?? 0;
            result.SpaceReclaimed += volumePrune.SpaceReclaimed;
        }

        return result;
    }

    /// <summary>
    /// Helper method to calculate human-readable uptime.
    /// </summary>
    private string CalculateUptime(DateTime created)
    {
        var uptime = DateTime.UtcNow - created;

        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";

        return $"{(int)uptime.TotalMinutes}m";
    }
}
