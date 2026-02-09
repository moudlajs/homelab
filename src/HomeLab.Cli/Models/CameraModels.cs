namespace HomeLab.Cli.Models;

public class CameraDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Online { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public bool SupportsSnapshot => Interfaces.Contains("Camera");
    public bool SupportsStreaming => Interfaces.Contains("VideoCamera");
    public bool HasMotionSensor => Interfaces.Contains("MotionSensor");
    public bool SupportsRecording => Interfaces.Contains("VideoRecorder");
}

public class CameraSystemStatus
{
    public bool IsOnline { get; set; }
    public string? Version { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int RecordingDevices { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
}

public class CameraStreamInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? RtspUrl { get; set; }
    public string? WebRtcUrl { get; set; }
    public string? ManagementUrl { get; set; }
}

public class CameraRecording
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
    public string? TriggerType { get; set; }
}
