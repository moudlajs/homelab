using Docker.DotNet;
using Docker.DotNet.Models;
using HomeLab.Cli.Models;

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
