namespace HomeLab.Cli.Models.AI;

/// <summary>
/// Snapshot of all collected homelab data for LLM analysis.
/// </summary>
public class HomelabDataSnapshot
{
    public SystemMetrics? System { get; set; }
    public DockerMetrics? Docker { get; set; }
    public PrometheusMetrics? Prometheus { get; set; }
    public NetworkMetrics? Network { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

public class SystemMetrics
{
    public int CpuCount { get; set; }
    public double CpuUsagePercent { get; set; }
    public double TotalMemoryGB { get; set; }
    public double UsedMemoryGB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public string DiskTotal { get; set; } = string.Empty;
    public string DiskUsed { get; set; } = string.Empty;
    public string DiskAvailable { get; set; } = string.Empty;
    public int DiskUsagePercent { get; set; }
    public string Uptime { get; set; } = string.Empty;
}

public class DockerMetrics
{
    public bool Available { get; set; }
    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public int StoppedContainers { get; set; }
    public List<ContainerSnapshot> Containers { get; set; } = new();
}

public class ContainerSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PrometheusMetrics
{
    public bool Available { get; set; }
    public int ActiveAlerts { get; set; }
    public List<string> AlertSummaries { get; set; } = new();
    public int TargetsUp { get; set; }
    public int TargetsDown { get; set; }
    public List<string> DownTargets { get; set; } = new();
}

public class NetworkMetrics
{
    // Nmap device discovery
    public bool NmapAvailable { get; set; }
    public int DevicesFound { get; set; }
    public List<DiscoveredDevice> Devices { get; set; } = new();

    // Ntopng traffic (when running)
    public bool NtopngAvailable { get; set; }
    public List<string> TopTalkers { get; set; } = new();
    public long TotalBytesTransferred { get; set; }
    public int ActiveFlows { get; set; }

    // Suricata IDS (when running)
    public bool SuricataAvailable { get; set; }
    public int SecurityAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int HighAlerts { get; set; }
    public List<string> AlertSummaries { get; set; } = new();
}

public class DiscoveredDevice
{
    public string Ip { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public List<int> OpenPorts { get; set; } = new();
}
