using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Camera;

public class ScryptedClient : IScryptedClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string? _token;

    public ScryptedClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;
        var serviceConfig = configService.GetServiceConfig("scrypted");
        _baseUrl = (serviceConfig.Url ?? "http://localhost:11080").TrimEnd('/');
        _username = serviceConfig.Username;
        _password = serviceConfig.Password;
        _token = serviceConfig.Token;
    }

    public string ServiceName => "Scrypted";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/login");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var healthy = await IsHealthyAsync();
            var devices = healthy ? await GetDevicesAsync() : new List<CameraDevice>();

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = healthy,
                Status = healthy ? "Running" : "Unavailable",
                Message = healthy ? "Scrypted is accessible" : "Scrypted is not reachable",
                Metrics = new Dictionary<string, string>
                {
                    { "Cameras", devices.Count.ToString() },
                    { "Online", devices.Count(d => d.Online).ToString() }
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = false,
                Status = "Error",
                Message = $"Failed to connect: {ex.Message}"
            };
        }
    }

    public async Task<List<CameraDevice>> GetDevicesAsync()
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, "/endpoint/@scrypted/core/public/");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var devices = new List<CameraDevice>();

            if (root.TryGetProperty("devices", out var devicesElement))
            {
                foreach (var prop in devicesElement.EnumerateObject())
                {
                    var dev = prop.Value;
                    var interfaces = new List<string>();
                    if (dev.TryGetProperty("interfaces", out var ifaces))
                    {
                        foreach (var iface in ifaces.EnumerateArray())
                        {
                            interfaces.Add(iface.GetString() ?? "");
                        }
                    }

                    // Only include camera-related devices
                    if (!interfaces.Contains("Camera") && !interfaces.Contains("VideoCamera"))
                    {
                        continue;
                    }

                    devices.Add(new CameraDevice
                    {
                        Id = prop.Name,
                        Name = dev.TryGetProperty("name", out var name) ? name.GetString() ?? prop.Name : prop.Name,
                        Type = dev.TryGetProperty("type", out var type) ? type.GetString() ?? "Camera" : "Camera",
                        Online = dev.TryGetProperty("online", out var online) && online.GetBoolean(),
                        Interfaces = interfaces,
                        Model = dev.TryGetProperty("info", out var info) && info.TryGetProperty("model", out var model) ? model.GetString() : null,
                        Manufacturer = info.ValueKind == JsonValueKind.Object && info.TryGetProperty("manufacturer", out var mfr) ? mfr.GetString() : null
                    });
                }
            }

            return devices;
        }
        catch
        {
            return new List<CameraDevice>();
        }
    }

    public async Task<CameraDevice?> GetDeviceAsync(string deviceId)
    {
        var devices = await GetDevicesAsync();
        return devices.FirstOrDefault(d =>
            d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase) ||
            d.Name.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CameraSystemStatus> GetSystemStatusAsync()
    {
        var healthy = await IsHealthyAsync();
        var devices = healthy ? await GetDevicesAsync() : new List<CameraDevice>();

        return new CameraSystemStatus
        {
            IsOnline = healthy,
            TotalDevices = devices.Count,
            OnlineDevices = devices.Count(d => d.Online),
            RecordingDevices = devices.Count(d => d.SupportsRecording),
            BaseUrl = _baseUrl
        };
    }

    public Task<CameraStreamInfo> GetStreamInfoAsync(string deviceId)
    {
        return Task.FromResult(new CameraStreamInfo
        {
            DeviceId = deviceId,
            DeviceName = deviceId,
            RtspUrl = $"rtsp://localhost:8554/{deviceId}",
            WebRtcUrl = $"{_baseUrl}/endpoint/@scrypted/webrtc/public/#/device/{deviceId}",
            ManagementUrl = $"{_baseUrl}/#/device/{deviceId}"
        });
    }

    public async Task<byte[]> TakeSnapshotAsync(string deviceId)
    {
        var request = CreateRequest(HttpMethod.Get, $"/endpoint/@scrypted/core/public/{deviceId}/snapshot");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<List<CameraRecording>> GetRecordingsAsync(string? deviceId = null, int limit = 20)
    {
        try
        {
            var path = deviceId != null
                ? $"/endpoint/@scrypted/core/public/{deviceId}/recordings?limit={limit}"
                : $"/endpoint/@scrypted/core/public/recordings?limit={limit}";

            var request = CreateRequest(HttpMethod.Get, path);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var recordings = new List<CameraRecording>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var rec in doc.RootElement.EnumerateArray())
                {
                    recordings.Add(new CameraRecording
                    {
                        Id = rec.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        DeviceId = rec.TryGetProperty("deviceId", out var did) ? did.GetString() ?? "" : "",
                        DeviceName = rec.TryGetProperty("deviceName", out var dn) ? dn.GetString() ?? "" : "",
                        StartTime = rec.TryGetProperty("startTime", out var st) ? DateTimeOffset.FromUnixTimeMilliseconds(st.GetInt64()).DateTime : DateTime.MinValue,
                        EndTime = rec.TryGetProperty("endTime", out var et) ? DateTimeOffset.FromUnixTimeMilliseconds(et.GetInt64()).DateTime : null,
                        TriggerType = rec.TryGetProperty("trigger", out var trig) ? trig.GetString() : null
                    });
                }
            }

            return recordings.Take(limit).ToList();
        }
        catch
        {
            return new List<CameraRecording>();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");

        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
        else if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{_username}:{_password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        return request;
    }
}
