using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

public interface IScryptedClient : IServiceClient
{
    Task<List<CameraDevice>> GetDevicesAsync();
    Task<CameraDevice?> GetDeviceAsync(string deviceId);
    Task<CameraSystemStatus> GetSystemStatusAsync();
    Task<CameraStreamInfo> GetStreamInfoAsync(string deviceId);
    Task<byte[]> TakeSnapshotAsync(string deviceId);
    Task<List<CameraRecording>> GetRecordingsAsync(string? deviceId = null, int limit = 20);
}
