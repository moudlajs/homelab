using HomeLab.Cli.Models.EventLog;

namespace HomeLab.Cli.Services.Network;

/// <summary>
/// Detects network anomalies by comparing event log snapshots.
/// </summary>
public interface INetworkAnomalyDetector
{
    /// <summary>
    /// Analyzes a sequence of event log entries and returns detected anomalies.
    /// </summary>
    List<NetworkAnomaly> DetectAnomalies(List<EventLogEntry> events);
}
