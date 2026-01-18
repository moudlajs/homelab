using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.LgTv;

/// <summary>
/// Client for controlling LG WebOS TVs via WebSocket API.
/// </summary>
public class LgTvClient : ILgTvClient
{
    private ClientWebSocket? _webSocket;
    private string? _clientKey;
    private int _messageId = 1;
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private static readonly int[] WebSocketPorts = { 3000, 3001 }; // 3000=ws, 3001=wss

    private const string RegisterPayload = @"{
        ""forcePairing"": false,
        ""pairingType"": ""PROMPT"",
        ""manifest"": {
            ""manifestVersion"": 1,
            ""appVersion"": ""1.1"",
            ""permissions"": [""CONTROL_POWER"", ""CONTROL_AUDIO"", ""LAUNCH"", ""READ_INSTALLED_APPS""]
        }
    }";

    public async Task<string?> ConnectAsync(string ipAddress, string? clientKey = null)
    {
        _clientKey = clientKey;
        Exception? lastException = null;

        // Try both ports (3000 for older TVs, 3001 for newer TVs with SSL)
        foreach (var port in WebSocketPorts)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                // Skip SSL certificate validation for self-signed TV certs
                if (port == 3001)
                {
                    _webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                }

                var scheme = port == 3001 ? "wss" : "ws";
                var uri = new Uri($"{scheme}://{ipAddress}:{port}");
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await _webSocket.ConnectAsync(uri, cts.Token);

                _ = ReceiveMessagesAsync();

                var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(RegisterPayload)!;
                if (!string.IsNullOrEmpty(_clientKey))
                {
                    payload["client-key"] = _clientKey;
                }

                var registerMessage = new { type = "register", id = "register_0", payload };
                await SendMessageAsync(registerMessage);

                var tcs = new TaskCompletionSource<JsonElement>();
                _pendingRequests["register_0"] = tcs;

                var response = await Task.WhenAny(tcs.Task, Task.Delay(30000));
                if (response != tcs.Task)
                {
                    throw new TimeoutException("Registration timed out. Did you accept the prompt on the TV?");
                }

                var result = await tcs.Task;
                if (result.TryGetProperty("client-key", out var keyElement))
                {
                    _clientKey = keyElement.GetString();
                }

                // If we got here but no client key, pairing wasn't accepted
                if (string.IsNullOrEmpty(_clientKey))
                {
                    throw new InvalidOperationException("TV responded but no client key received. Did you accept the pairing prompt on TV?");
                }

                return _clientKey;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await DisconnectAsync();
                // Try next port
            }
        }

        throw lastException ?? new Exception("Failed to connect to TV on any port");
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
            catch { }
        }
        _webSocket?.Dispose();
        _webSocket = null;
    }

    public async Task<bool> IsPoweredOnAsync(string ipAddress)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 2000);
            if (reply.Status != IPStatus.Success)
            {
                return false;
            }

            // Try both ports
            foreach (var port in WebSocketPorts)
            {
                using var testSocket = new ClientWebSocket();
                if (port == 3001)
                {
                    testSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                }

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var scheme = port == 3001 ? "wss" : "ws";
                try
                {
                    await testSocket.ConnectAsync(new Uri($"{scheme}://{ipAddress}:{port}"), cts.Token);
                    return true;
                }
                catch { /* try next port */ }
            }

            // If ping worked but WebSocket didn't, TV might still be on (just WebOS not responding)
            return true;
        }
        catch { return false; }
    }

    public async Task PowerOffAsync() => await SendRequestAsync("ssap://system/turnOff", null);
    public async Task SetVolumeAsync(int volume) => await SendRequestAsync("ssap://audio/setVolume", new { volume });
    public async Task SetMuteAsync(bool mute) => await SendRequestAsync("ssap://audio/setMute", new { mute });
    public async Task LaunchAppAsync(string appId) => await SendRequestAsync("ssap://system.launcher/launch", new { id = appId });

    public async Task<int> GetVolumeAsync()
    {
        var response = await SendRequestAsync("ssap://audio/getVolume", null);
        return response.TryGetProperty("volume", out var v) ? v.GetInt32() : 0;
    }

    public async Task<List<TvApp>> GetAppsAsync()
    {
        var response = await SendRequestAsync("ssap://com.webos.applicationManager/listLaunchPoints", null);
        var apps = new List<TvApp>();
        if (response.TryGetProperty("launchPoints", out var launchPoints))
        {
            foreach (var app in launchPoints.EnumerateArray())
            {
                apps.Add(new TvApp { Id = app.GetProperty("id").GetString() ?? "", Name = app.GetProperty("title").GetString() ?? "" });
            }
        }

        return apps;
    }

    private async Task<JsonElement> SendRequestAsync(string uri, object? payload)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Not connected to TV");
        }

        var id = $"request_{_messageId++}";
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingRequests[id] = tcs;

        await SendMessageAsync(new { type = "request", id, uri, payload });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));
        if (completed != tcs.Task) { _pendingRequests.Remove(id); throw new TimeoutException("Request timed out"); }
        return await tcs.Task;
    }

    private async Task SendMessageAsync(object message)
    {
        if (_webSocket == null)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];
        try
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                ProcessMessage(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
        }
        catch { }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();
                if (id != null && _pendingRequests.TryGetValue(id, out var tcs))
                {
                    _pendingRequests.Remove(id);
                    tcs.SetResult(root.TryGetProperty("payload", out var p) ? p.Clone() : root.Clone());
                }
            }
        }
        catch { }
    }
}
