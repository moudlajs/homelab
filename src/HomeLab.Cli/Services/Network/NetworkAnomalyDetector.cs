using HomeLab.Cli.Models.EventLog;

namespace HomeLab.Cli.Services.Network;

/// <summary>
/// Pure-logic anomaly detector that compares consecutive network snapshots.
/// No external dependencies — fully testable.
/// </summary>
public class NetworkAnomalyDetector : INetworkAnomalyDetector
{
    public List<NetworkAnomaly> DetectAnomalies(List<EventLogEntry> events)
    {
        var anomalies = new List<NetworkAnomaly>();

        if (events.Count < 2)
        {
            return anomalies;
        }

        for (var i = 1; i < events.Count; i++)
        {
            var prev = events[i - 1];
            var curr = events[i];

            DetectNewDevices(prev, curr, anomalies);
            DetectDeviceDisappearances(prev, curr, i, events, anomalies);
            DetectTrafficSpikes(i, events, anomalies);
            DetectSecurityAlerts(curr, anomalies);
            DetectDeviceCountAnomaly(i, events, anomalies);
        }

        return anomalies;
    }

    private static void DetectNewDevices(EventLogEntry prev, EventLogEntry curr, List<NetworkAnomaly> anomalies)
    {
        var prevIps = prev.Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();
        var currDevices = curr.Network?.Devices ?? new List<DeviceBrief>();

        foreach (var device in currDevices)
        {
            if (!prevIps.Contains(device.Ip))
            {
                var info = device.Hostname ?? device.Vendor ?? "unknown";
                anomalies.Add(new NetworkAnomaly
                {
                    Timestamp = curr.Timestamp,
                    Type = "NewDevice",
                    Severity = "warning",
                    Description = $"New device: {device.Ip} ({info})",
                    Details = new Dictionary<string, string>
                    {
                        ["ip"] = device.Ip,
                        ["mac"] = device.Mac ?? "",
                        ["hostname"] = device.Hostname ?? "",
                        ["vendor"] = device.Vendor ?? ""
                    }
                });
            }
        }
    }

    private static void DetectDeviceDisappearances(
        EventLogEntry prev, EventLogEntry curr, int index,
        List<EventLogEntry> events, List<NetworkAnomaly> anomalies)
    {
        var prevIps = prev.Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();
        var currIps = curr.Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();

        foreach (var ip in prevIps.Except(currIps))
        {
            // Only flag if device was seen in 3+ consecutive prior snapshots
            var consecutiveCount = 0;
            for (var j = index - 1; j >= 0 && j >= index - 4; j--)
            {
                var ips = events[j].Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();
                if (ips.Contains(ip))
                {
                    consecutiveCount++;
                }
                else
                {
                    break;
                }
            }

            if (consecutiveCount >= 3)
            {
                anomalies.Add(new NetworkAnomaly
                {
                    Timestamp = curr.Timestamp,
                    Type = "DeviceGone",
                    Severity = "info",
                    Description = $"Device left network: {ip}",
                    Details = new Dictionary<string, string> { ["ip"] = ip }
                });
            }
        }
    }

    private static void DetectTrafficSpikes(int index, List<EventLogEntry> events, List<NetworkAnomaly> anomalies)
    {
        var curr = events[index];
        var currBytes = curr.Network?.Traffic?.TotalBytes ?? 0;
        if (currBytes == 0)
        {
            return;
        }

        // Calculate rolling average of last 6 snapshots
        var windowStart = Math.Max(0, index - 6);
        var trafficValues = new List<long>();
        for (var j = windowStart; j < index; j++)
        {
            var bytes = events[j].Network?.Traffic?.TotalBytes ?? 0;
            if (bytes > 0)
            {
                trafficValues.Add(bytes);
            }
        }

        if (trafficValues.Count < 2)
        {
            return;
        }

        var avg = trafficValues.Average();
        if (avg > 0 && currBytes > avg * 3)
        {
            anomalies.Add(new NetworkAnomaly
            {
                Timestamp = curr.Timestamp,
                Type = "TrafficSpike",
                Severity = "warning",
                Description = $"Traffic spike: {FormatBytes(currBytes)} (avg: {FormatBytes((long)avg)})",
                Details = new Dictionary<string, string>
                {
                    ["currentBytes"] = currBytes.ToString(),
                    ["averageBytes"] = ((long)avg).ToString()
                }
            });
        }
    }

    private static void DetectSecurityAlerts(EventLogEntry curr, List<NetworkAnomaly> anomalies)
    {
        var security = curr.Network?.Security;
        if (security == null || security.TotalAlerts == 0)
        {
            return;
        }

        if (security.CriticalCount > 0)
        {
            var topAlert = security.RecentAlerts.FirstOrDefault(a => a.Severity == "critical");
            var sig = topAlert?.Signature ?? "unknown";
            anomalies.Add(new NetworkAnomaly
            {
                Timestamp = curr.Timestamp,
                Type = "SecurityAlert",
                Severity = "critical",
                Description = $"{security.CriticalCount} critical alert(s): {sig}",
                Details = new Dictionary<string, string>
                {
                    ["criticalCount"] = security.CriticalCount.ToString(),
                    ["highCount"] = security.HighCount.ToString(),
                    ["signature"] = sig
                }
            });
        }
        else if (security.HighCount > 0)
        {
            var topAlert = security.RecentAlerts.FirstOrDefault(a => a.Severity == "high");
            var sig = topAlert?.Signature ?? "unknown";
            anomalies.Add(new NetworkAnomaly
            {
                Timestamp = curr.Timestamp,
                Type = "SecurityAlert",
                Severity = "warning",
                Description = $"{security.HighCount} high severity alert(s): {sig}",
                Details = new Dictionary<string, string>
                {
                    ["highCount"] = security.HighCount.ToString(),
                    ["signature"] = sig
                }
            });
        }
    }

    private static void DetectDeviceCountAnomaly(int index, List<EventLogEntry> events, List<NetworkAnomaly> anomalies)
    {
        var curr = events[index];
        var currCount = curr.Network?.DeviceCount ?? 0;
        if (currCount == 0)
        {
            return;
        }

        var windowStart = Math.Max(0, index - 6);
        var counts = new List<int>();
        for (var j = windowStart; j < index; j++)
        {
            var count = events[j].Network?.DeviceCount ?? 0;
            if (count > 0)
            {
                counts.Add(count);
            }
        }

        if (counts.Count < 3)
        {
            return;
        }

        var avg = counts.Average();
        if (avg > 0 && Math.Abs(currCount - avg) > avg * 0.3)
        {
            var direction = currCount > avg ? "increase" : "decrease";
            anomalies.Add(new NetworkAnomaly
            {
                Timestamp = curr.Timestamp,
                Type = "DeviceCountAnomaly",
                Severity = "warning",
                Description = $"Device count {direction}: {currCount} (avg: {avg:F0})",
                Details = new Dictionary<string, string>
                {
                    ["currentCount"] = currCount.ToString(),
                    ["averageCount"] = avg.ToString("F0")
                }
            });
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.#} {sizes[order]}";
    }
}
