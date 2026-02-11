namespace HomeLab.Cli.Models.EventLog;

/// <summary>
/// A single event log entry written as one JSON line in events.jsonl.
/// Captures lightweight system state for historical analysis.
/// </summary>
public class EventLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SystemSnapshot? System { get; set; }
    public PowerSnapshot? Power { get; set; }
    public TailscaleSnapshot? Tailscale { get; set; }
    public DockerSnapshot? Docker { get; set; }
    public NetworkSnapshot? Network { get; set; }
    public List<ServiceHealthEntry> Services { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class SystemSnapshot
{
    public double CpuPercent { get; set; }
    public double MemoryPercent { get; set; }
    public int DiskPercent { get; set; }
    public string Uptime { get; set; } = string.Empty;
}

public class PowerSnapshot
{
    public List<PowerEvent> RecentEvents { get; set; } = new();
}

public class PowerEvent
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty; // Sleep, Wake, DarkWake
}

public class TailscaleSnapshot
{
    public bool IsConnected { get; set; }
    public string BackendState { get; set; } = string.Empty;
    public string? SelfIp { get; set; }
    public int PeerCount { get; set; }
    public int OnlinePeerCount { get; set; }
}

public class DockerSnapshot
{
    public bool Available { get; set; }
    public int RunningCount { get; set; }
    public int TotalCount { get; set; }
    public List<ContainerBrief> Containers { get; set; } = new();
}

public class ContainerBrief
{
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
}

public class NetworkSnapshot
{
    public int DeviceCount { get; set; }
}

public class ServiceHealthEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}
