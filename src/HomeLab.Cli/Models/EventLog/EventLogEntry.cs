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
    public SpeedtestSnapshot? Speedtest { get; set; }
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
    public List<DeviceBrief> Devices { get; set; } = new();
    public TrafficSummary? Traffic { get; set; }
    public SecuritySummary? Security { get; set; }
}

public class DeviceBrief
{
    public string Ip { get; set; } = string.Empty;
    public string? Mac { get; set; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
}

public class TrafficSummary
{
    public long TotalBytes { get; set; }
    public int ActiveFlows { get; set; }
    public List<TopTalkerBrief> TopTalkers { get; set; } = new();
}

public class TopTalkerBrief
{
    public string Ip { get; set; } = string.Empty;
    public string? Name { get; set; }
    public long TotalBytes { get; set; }
}

public class SecuritySummary
{
    public int TotalAlerts { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public List<AlertBrief> RecentAlerts { get; set; } = new();
}

public class AlertBrief
{
    public string Severity { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public string DestinationIp { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class NetworkAnomaly
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Details { get; set; } = new();
}

public class SpeedtestSnapshot
{
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double PingMs { get; set; }
    public string Server { get; set; } = string.Empty;
    public string Isp { get; set; } = string.Empty;
}

public class ServiceHealthEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}
