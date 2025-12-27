namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a dependency relationship between services.
/// </summary>
public class ServiceDependency
{
    /// <summary>
    /// The service that depends on others.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Services that this service depends on.
    /// </summary>
    public List<string> DependsOn { get; set; } = new();

    /// <summary>
    /// Dependency type (e.g., "hard" or "soft").
    /// Hard = service won't work without it
    /// Soft = service works better with it
    /// </summary>
    public DependencyType Type { get; set; } = DependencyType.Hard;

    /// <summary>
    /// Description of why this dependency exists.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Type of dependency between services.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Service requires this dependency to function.
    /// </summary>
    Hard,

    /// <summary>
    /// Service works better with this dependency but can function without it.
    /// </summary>
    Soft
}
