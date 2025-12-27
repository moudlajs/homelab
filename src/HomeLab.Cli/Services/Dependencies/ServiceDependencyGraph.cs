using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Dependencies;

/// <summary>
/// Manages service dependencies and provides dependency resolution.
/// </summary>
public class ServiceDependencyGraph
{
    private readonly Dictionary<string, ServiceDependency> _dependencies = new();

    /// <summary>
    /// Initializes the dependency graph with known homelab service dependencies.
    /// </summary>
    public ServiceDependencyGraph()
    {
        InitializeHomelabDependencies();
    }

    /// <summary>
    /// Defines the standard homelab service dependencies.
    /// </summary>
    private void InitializeHomelabDependencies()
    {
        // Grafana depends on Prometheus
        AddDependency(new ServiceDependency
        {
            ServiceName = "grafana",
            DependsOn = new List<string> { "prometheus" },
            Type = DependencyType.Hard,
            Reason = "Grafana requires Prometheus as a data source"
        });

        // Prometheus could benefit from node-exporter
        AddDependency(new ServiceDependency
        {
            ServiceName = "prometheus",
            DependsOn = new List<string> { "node-exporter" },
            Type = DependencyType.Soft,
            Reason = "Prometheus collects metrics from node-exporter"
        });

        // AdGuard is independent (no dependencies)
        // WireGuard is independent (no dependencies)
        // Node Exporter is independent (no dependencies)
    }

    /// <summary>
    /// Adds a dependency to the graph.
    /// </summary>
    public void AddDependency(ServiceDependency dependency)
    {
        _dependencies[dependency.ServiceName.ToLowerInvariant()] = dependency;
    }

    /// <summary>
    /// Gets the dependencies for a service.
    /// </summary>
    public ServiceDependency? GetDependencies(string serviceName)
    {
        _dependencies.TryGetValue(serviceName.ToLowerInvariant(), out var dependency);
        return dependency;
    }

    /// <summary>
    /// Gets all dependencies in the graph.
    /// </summary>
    public IEnumerable<ServiceDependency> GetAllDependencies()
    {
        return _dependencies.Values;
    }

    /// <summary>
    /// Gets the startup order for services based on dependencies.
    /// Uses topological sort to ensure dependencies start before dependents.
    /// </summary>
    public List<string> GetStartupOrder(IEnumerable<string> services)
    {
        var serviceList = services.Select(s => s.ToLowerInvariant()).ToList();
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var service in serviceList)
        {
            if (!visited.Contains(service))
            {
                TopologicalSort(service, serviceList, visited, visiting, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Performs topological sort for dependency resolution.
    /// </summary>
    private void TopologicalSort(
        string service,
        List<string> allServices,
        HashSet<string> visited,
        HashSet<string> visiting,
        List<string> result)
    {
        if (visiting.Contains(service))
        {
            // Circular dependency detected - skip
            return;
        }

        if (visited.Contains(service))
        {
            return;
        }

        visiting.Add(service);

        var dependency = GetDependencies(service);
        if (dependency != null)
        {
            foreach (var dep in dependency.DependsOn)
            {
                var depLower = dep.ToLowerInvariant();
                // Only process if the dependency is in our service list
                if (allServices.Contains(depLower))
                {
                    TopologicalSort(depLower, allServices, visited, visiting, result);
                }
            }
        }

        visiting.Remove(service);
        visited.Add(service);
        result.Add(service);
    }

    /// <summary>
    /// Checks if all dependencies for a service are healthy.
    /// </summary>
    public bool AreDependenciesHealthy(
        string serviceName,
        Dictionary<string, bool> serviceHealthStatus)
    {
        var dependency = GetDependencies(serviceName);
        if (dependency == null)
        {
            // No dependencies, so all are "healthy"
            return true;
        }

        foreach (var dep in dependency.DependsOn)
        {
            var depLower = dep.ToLowerInvariant();

            // For hard dependencies, they must be healthy
            if (dependency.Type == DependencyType.Hard)
            {
                if (!serviceHealthStatus.TryGetValue(depLower, out var isHealthy) || !isHealthy)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a visual representation of the dependency graph.
    /// </summary>
    public List<string> GetDependencyVisualization()
    {
        var lines = new List<string>();

        foreach (var dep in _dependencies.Values.OrderBy(d => d.ServiceName))
        {
            var arrow = dep.Type == DependencyType.Hard ? "==>" : "-->";
            var depList = string.Join(", ", dep.DependsOn);
            lines.Add($"{dep.ServiceName} {arrow} [{depList}]");

            if (!string.IsNullOrEmpty(dep.Reason))
            {
                lines.Add($"  └─ {dep.Reason}");
            }
        }

        return lines;
    }

    /// <summary>
    /// Gets services that depend on a specific service.
    /// </summary>
    public List<string> GetDependents(string serviceName)
    {
        var serviceNameLower = serviceName.ToLowerInvariant();
        return _dependencies.Values
            .Where(d => d.DependsOn.Any(dep => dep.ToLowerInvariant() == serviceNameLower))
            .Select(d => d.ServiceName)
            .ToList();
    }
}
