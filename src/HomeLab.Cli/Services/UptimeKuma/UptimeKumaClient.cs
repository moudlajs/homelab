using System.Text.Json;
using HomeLab.Cli.Services.Abstractions;
using SocketIOClient;

namespace HomeLab.Cli.Services.UptimeKuma;

/// <summary>
/// Client for interacting with Uptime Kuma via socket.io API.
/// </summary>
public class UptimeKumaClient : IDisposable
{
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private SocketIOClient.SocketIO? _socket;

    public UptimeKumaClient(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _username = username;
        _password = password;
    }

    private async Task ConnectAsync()
    {
        _socket?.Dispose();
        _socket = new SocketIOClient.SocketIO(new Uri(_baseUrl), new SocketIOOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            Reconnection = false
        });

        await _socket.ConnectAsync();
    }

    private async Task LoginAsync()
    {
        var loginTcs = new TaskCompletionSource<bool>();

        await _socket!.EmitAsync("login",
            new object[] { new { username = _username, password = _password, token = "" } },
            (SocketIOClient.Common.Messages.IDataMessage ack) =>
            {
                var json = ack.GetValue<JsonElement>(0);
                var ok = json.GetProperty("ok").GetBoolean();
                if (!ok)
                {
                    var msg = json.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : "Unknown error";
                    loginTcs.TrySetException(new InvalidOperationException($"Login failed: {msg}"));
                }
                else
                {
                    loginTcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        if (await Task.WhenAny(loginTcs.Task, timeout) == timeout)
        {
            throw new TimeoutException("Login to Uptime Kuma timed out");
        }

        await loginTcs.Task;
    }

    /// <summary>
    /// Get health check for Uptime Kuma service.
    /// Just tests TCP connectivity, doesn't consume events.
    /// </summary>
    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync(_baseUrl);
            return new ServiceHealthInfo { IsHealthy = true, Message = "Uptime Kuma is running" };
        }
        catch (Exception ex)
        {
            return new ServiceHealthInfo
            {
                IsHealthy = false,
                Message = $"Failed to connect: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get all monitored services with their current status.
    /// Creates socket, registers event handlers, then logs in to trigger data broadcast.
    /// </summary>
    public async Task<List<UptimeMonitor>> GetMonitorsAsync()
    {
        _socket?.Dispose();
        _socket = new SocketIOClient.SocketIO(new Uri(_baseUrl), new SocketIOOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            Reconnection = false
        });

        var monitorTcs = new TaskCompletionSource<List<UptimeMonitor>>();
        var heartbeats = new Dictionary<int, int>();
        var pings = new Dictionary<int, int>();
        var uptimes = new Dictionary<int, decimal>(); // monitorId -> 24h uptime
        var heartbeatCount = 0;
        var expectedCount = 0;

        // Register ALL handlers BEFORE connect so we catch everything
        _socket.OnAny((eventName, ctx) =>
        {
            try
            {
                if (eventName == "monitorList")
                {
                    var json = ctx.GetValue<JsonElement>(0);
                    var monitors = new List<UptimeMonitor>();

                    foreach (var prop in json.EnumerateObject())
                    {
                        var m = prop.Value;
                        monitors.Add(new UptimeMonitor
                        {
                            Id = m.GetProperty("id").GetInt32(),
                            Name = m.GetProperty("name").GetString() ?? "",
                            Url = m.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
                            Type = m.TryGetProperty("type", out var type) ? type.GetString() ?? "http" : "http",
                            Active = m.TryGetProperty("active", out var active) && active.GetBoolean()
                        });
                    }

                    expectedCount = monitors.Count;
                    monitorTcs.TrySetResult(monitors);
                }
                else if (eventName == "heartbeatList")
                {
                    var monitorIdStr = ctx.GetValue<string>(0) ?? "0";
                    var monitorId = int.Parse(monitorIdStr);
                    var data = ctx.GetValue<JsonElement>(1);
                    var beats = data.EnumerateArray().ToList();
                    if (beats.Count > 0)
                    {
                        var latest = beats.Last();
                        heartbeats[monitorId] = latest.GetProperty("status").GetInt32();
                        if (latest.TryGetProperty("ping", out var ping) && ping.ValueKind == JsonValueKind.Number)
                        {
                            pings[monitorId] = ping.GetInt32();
                        }
                    }
                    heartbeatCount++;
                }
                else if (eventName == "uptime")
                {
                    var monitorIdStr = ctx.GetValue<string>(0) ?? "0";
                    var monitorId = int.Parse(monitorIdStr);
                    var period = ctx.GetValue<int>(1);
                    var value = ctx.GetValue<decimal>(2);
                    if (period == 24)
                    {
                        uptimes[monitorId] = value;
                    }
                }
            }
            catch { }
            return Task.CompletedTask;
        });

        // Connect then login — events fire with handlers already in place
        await _socket.ConnectAsync();
        await LoginAsync();

        // Wait for monitor list
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        if (await Task.WhenAny(monitorTcs.Task, timeout) == timeout)
        {
            throw new TimeoutException("Timed out waiting for monitor list");
        }

        var monitorList = await monitorTcs.Task;

        // Wait for heartbeats to arrive (they come right after monitorList)
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && heartbeatCount < expectedCount)
        {
            await Task.Delay(100);
        }

        // Merge data into monitors
        foreach (var monitor in monitorList)
        {
            if (heartbeats.TryGetValue(monitor.Id, out var status))
            {
                monitor.Status = status == 1 ? MonitorStatus.Up : MonitorStatus.Down;
            }

            if (pings.TryGetValue(monitor.Id, out var ping))
            {
                monitor.AverageResponse = ping;
            }

            if (uptimes.TryGetValue(monitor.Id, out var uptime))
            {
                monitor.UptimePercentage = Math.Round(uptime * 100, 2);
            }
        }

        return monitorList;
    }

    /// <summary>
    /// Get recent incidents (monitors currently down).
    /// </summary>
    public async Task<List<UptimeIncident>> GetIncidentsAsync(int limit = 10)
    {
        var monitors = await GetMonitorsAsync();
        var incidents = new List<UptimeIncident>();

        foreach (var monitor in monitors.Where(m => m.Status == MonitorStatus.Down))
        {
            incidents.Add(new UptimeIncident
            {
                Id = monitor.Id,
                MonitorName = monitor.Name,
                Status = "down",
                Message = $"{monitor.Name} is not responding",
                StartedAt = DateTime.Now,
                Duration = TimeSpan.Zero
            });
        }

        return incidents.Take(limit).ToList();
    }

    /// <summary>
    /// Add a new monitor.
    /// </summary>
    public async Task<bool> AddMonitorAsync(string name, string url, string type = "http")
    {
        await ConnectAsync();
        await LoginAsync();

        var tcs = new TaskCompletionSource<bool>();

        await _socket!.EmitAsync("add",
            new object[]
            {
                new
                {
                    name, url, type,
                    interval = 60,
                    maxretries = 3,
                    accepted_statuscodes = new[] { "200-299", "300-399" }
                }
            },
            (SocketIOClient.Common.Messages.IDataMessage ack) =>
            {
                try
                {
                    var json = ack.GetValue<JsonElement>(0);
                    tcs.TrySetResult(json.GetProperty("ok").GetBoolean());
                }
                catch
                {
                    tcs.TrySetResult(false);
                }
                return Task.CompletedTask;
            });

        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        if (await Task.WhenAny(tcs.Task, timeout) == timeout)
        {
            return false;
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Remove a monitor by ID.
    /// </summary>
    public async Task<bool> RemoveMonitorAsync(int id)
    {
        await ConnectAsync();
        await LoginAsync();

        var tcs = new TaskCompletionSource<bool>();

        await _socket!.EmitAsync("deleteMonitor",
            new object[] { id },
            (SocketIOClient.Common.Messages.IDataMessage ack) =>
            {
                try
                {
                    var json = ack.GetValue<JsonElement>(0);
                    tcs.TrySetResult(json.GetProperty("ok").GetBoolean());
                }
                catch
                {
                    tcs.TrySetResult(false);
                }
                return Task.CompletedTask;
            });

        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        if (await Task.WhenAny(tcs.Task, timeout) == timeout)
        {
            return false;
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Get a monitor by ID.
    /// </summary>
    public async Task<UptimeMonitor?> GetMonitorAsync(int id)
    {
        var monitors = await GetMonitorsAsync();
        return monitors.FirstOrDefault(m => m.Id == id);
    }

    public void Dispose()
    {
        if (_socket != null)
        {
            _socket.DisconnectAsync().Wait(TimeSpan.FromSeconds(2));
            _socket.Dispose();
            _socket = null;
        }
    }
}

public class UptimeMonitor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "http";
    public MonitorStatus Status { get; set; } = MonitorStatus.Unknown;
    public decimal UptimePercentage { get; set; }
    public int AverageResponse { get; set; }
    public bool Active { get; set; }
}

public enum MonitorStatus
{
    Up = 1,
    Down = 0,
    Unknown = -1
}

public class UptimeIncident
{
    public int Id { get; set; }
    public string MonitorName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
