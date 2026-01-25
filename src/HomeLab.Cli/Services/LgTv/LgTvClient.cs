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
    private Action<string>? _logAction;

    private const string RegisterPayload = @"{
        ""forcePairing"": false,
        ""pairingType"": ""PROMPT"",
        ""manifest"": {
            ""manifestVersion"": 1,
            ""appVersion"": ""1.1"",
            ""permissions"": [
                ""CONTROL_POWER"",
                ""CONTROL_AUDIO"",
                ""CONTROL_INPUT_TV"",
                ""CONTROL_INPUT_MEDIA_PLAYBACK"",
                ""CONTROL_MOUSE_AND_KEYBOARD"",
                ""CONTROL_CHANNEL_UP_DOWN"",
                ""LAUNCH"",
                ""READ_INSTALLED_APPS"",
                ""READ_TV_CHANNEL_LIST"",
                ""READ_CURRENT_CHANNEL"",
                ""READ_INPUT_DEVICE_LIST"",
                ""READ_RUNNING_APPS"",
                ""WRITE_NOTIFICATION_TOAST""
            ]
        }
    }";

    /// <summary>
    /// Sets the logging action for verbose output.
    /// </summary>
    public void SetVerboseLogging(Action<string> logAction) => _logAction = logAction;

    private void Log(string message)
    {
        _logAction?.Invoke(message);
    }

    public async Task<string?> ConnectAsync(string ipAddress, string? clientKey = null)
    {
        _clientKey = clientKey;
        Exception? lastException = null;

        Log($"Starting connection to {ipAddress}");
        Log($"Existing client key: {(string.IsNullOrEmpty(clientKey) ? "(none - will need pairing)" : "(provided)")}");

        // Try both ports (3000 for older TVs, 3001 for newer TVs with SSL)
        foreach (var port in WebSocketPorts)
        {
            var scheme = port == 3001 ? "wss" : "ws";
            var uri = new Uri($"{scheme}://{ipAddress}:{port}");
            Log($"Trying {uri}...");

            try
            {
                _webSocket = new ClientWebSocket();
                // Skip SSL certificate validation for self-signed TV certs
                if (port == 3001)
                {
                    _webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                    Log("  SSL certificate validation disabled for self-signed cert");
                }

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await _webSocket.ConnectAsync(uri, cts.Token);
                Log($"  WebSocket connected successfully");

                _ = ReceiveMessagesAsync();

                var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(RegisterPayload)!;
                if (!string.IsNullOrEmpty(_clientKey))
                {
                    payload["client-key"] = _clientKey;
                    Log("  Including existing client key in registration");
                }

                var registerMessage = new { type = "register", id = "register_0", payload };
                var jsonToSend = JsonSerializer.Serialize(registerMessage, new JsonSerializerOptions { WriteIndented = true });
                Log($"  Sending registration message:\n{jsonToSend}");
                await SendMessageAsync(registerMessage);

                var tcs = new TaskCompletionSource<JsonElement>();
                _pendingRequests["register_0"] = tcs;

                Log("  Waiting for TV response (30s timeout)...");
                Log("  >>> Check your TV screen for pairing prompt! <<<");

                var response = await Task.WhenAny(tcs.Task, Task.Delay(30000));
                if (response != tcs.Task)
                {
                    Log("  TIMEOUT - No response from TV");
                    throw new TimeoutException("Registration timed out. Did you accept the prompt on the TV?");
                }

                var result = await tcs.Task;
                var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                Log($"  Received response:\n{resultJson}");

                // Check for client key in first response
                if (result.TryGetProperty("client-key", out var keyElement))
                {
                    _clientKey = keyElement.GetString();
                    Log($"  Got client key: {_clientKey?[..Math.Min(8, _clientKey?.Length ?? 0)]}...");
                }

                // If no client key yet, TV is showing prompt - wait for second response after user accepts
                if (string.IsNullOrEmpty(_clientKey))
                {
                    // Check if TV acknowledged the prompt request
                    if (result.TryGetProperty("pairingType", out var pairingType) &&
                        pairingType.GetString() == "PROMPT")
                    {
                        Log("  TV acknowledged prompt request, waiting for user to accept on TV...");
                        Log("  >>> LOOK AT YOUR TV SCREEN AND ACCEPT THE PAIRING PROMPT! <<<");

                        // Wait for the second response with the client key
                        var tcs2 = new TaskCompletionSource<JsonElement>();
                        _pendingRequests["register_0"] = tcs2;

                        var response2 = await Task.WhenAny(tcs2.Task, Task.Delay(30000));
                        if (response2 != tcs2.Task)
                        {
                            Log("  TIMEOUT waiting for pairing acceptance");
                            throw new TimeoutException("Pairing timed out. Did you accept the prompt on the TV?");
                        }

                        var result2 = await tcs2.Task;
                        var result2Json = JsonSerializer.Serialize(result2, new JsonSerializerOptions { WriteIndented = true });
                        Log($"  Received second response:\n{result2Json}");

                        if (result2.TryGetProperty("client-key", out var keyElement2))
                        {
                            _clientKey = keyElement2.GetString();
                            Log($"  Got client key: {_clientKey?[..Math.Min(8, _clientKey?.Length ?? 0)]}...");
                        }
                    }
                }

                // Final check for client key
                if (string.IsNullOrEmpty(_clientKey))
                {
                    Log("  ERROR: No client key in response");
                    throw new InvalidOperationException("TV responded but no client key received. Did you accept the pairing prompt on TV?");
                }

                Log("  SUCCESS - Pairing complete!");
                return _clientKey;
            }
            catch (Exception ex)
            {
                Log($"  FAILED: {ex.GetType().Name}: {ex.Message}");
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

    /// <summary>
    /// Get the currently running foreground app.
    /// </summary>
    public async Task<string?> GetForegroundAppAsync()
    {
        var response = await SendRequestAsync("ssap://com.webos.applicationManager/getForegroundAppInfo", null);
        if (response.TryGetProperty("appId", out var appIdElement))
        {
            return appIdElement.GetString();
        }
        return null;
    }

    /// <summary>
    /// Wait for a specific app to be in the foreground.
    /// </summary>
    public async Task<bool> WaitForAppAsync(string appId, int timeoutSeconds = 30)
    {
        var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
        while (DateTime.Now < deadline)
        {
            try
            {
                var currentApp = await GetForegroundAppAsync();
                Log($"  Current foreground app: {currentApp}");
                if (currentApp != null && currentApp.Equals(appId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore errors during polling
            }
            await Task.Delay(1000);
        }
        return false;
    }

    /// <summary>
    /// Send a remote control key press to the TV.
    /// </summary>
    public async Task SendKeyAsync(string key)
    {
        // Get the pointer input socket
        var response = await SendRequestAsync("ssap://com.webos.service.networkinput/getPointerInputSocket", null);
        if (!response.TryGetProperty("socketPath", out var socketPathElement))
        {
            throw new InvalidOperationException("Could not get input socket path");
        }

        var socketPath = socketPathElement.GetString();
        if (string.IsNullOrEmpty(socketPath))
        {
            throw new InvalidOperationException("Empty input socket path");
        }

        Log($"  Got input socket: {socketPath}");

        // Connect to the input socket
        using var inputSocket = new ClientWebSocket();
        inputSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        await inputSocket.ConnectAsync(new Uri(socketPath), CancellationToken.None);
        Log($"  Connected to input socket");

        // Send the key press (format: type:button\nname:KEY\n\n)
        var keyCommand = $"type:button\nname:{key.ToUpper()}\n\n";
        var keyBytes = Encoding.UTF8.GetBytes(keyCommand);
        await inputSocket.SendAsync(keyBytes, WebSocketMessageType.Text, true, CancellationToken.None);
        Log($"  Sent key: {key.ToUpper()}");

        // Small delay to let the TV process the key
        await Task.Delay(100);

        await inputSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    /// <summary>
    /// Common remote control keys.
    /// </summary>
    public static class Keys
    {
        public const string Enter = "ENTER";
        public const string Back = "BACK";
        public const string Home = "HOME";
        public const string Up = "UP";
        public const string Down = "DOWN";
        public const string Left = "LEFT";
        public const string Right = "RIGHT";
        public const string Play = "PLAY";
        public const string Pause = "PAUSE";
        public const string Stop = "STOP";
        public const string VolumeUp = "VOLUMEUP";
        public const string VolumeDown = "VOLUMEDOWN";
        public const string Mute = "MUTE";
        public const string ChannelUp = "CHANNELUP";
        public const string ChannelDown = "CHANNELDOWN";
        public const string Red = "RED";
        public const string Green = "GREEN";
        public const string Yellow = "YELLOW";
        public const string Blue = "BLUE";
        public const string Num0 = "0";
        public const string Num1 = "1";
        public const string Num2 = "2";
        public const string Num3 = "3";
        public const string Num4 = "4";
        public const string Num5 = "5";
        public const string Num6 = "6";
        public const string Num7 = "7";
        public const string Num8 = "8";
        public const string Num9 = "9";
    }

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
        var messageBuffer = new List<byte>();

        try
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // Append chunk to message buffer
                messageBuffer.AddRange(buffer.Take(result.Count));

                // Only process when we have the complete message
                if (result.EndOfMessage)
                {
                    var fullMessage = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();
                    ProcessMessage(fullMessage);
                }
            }
        }
        catch { }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            Log($"  [RECV] {json}");
            var root = JsonDocument.Parse(json).RootElement;

            // Log message type if present
            if (root.TryGetProperty("type", out var typeElement))
            {
                Log($"  Message type: {typeElement.GetString()}");
            }

            if (root.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();
                Log($"  Message ID: {id}");
                if (id != null && _pendingRequests.TryGetValue(id, out var tcs))
                {
                    _pendingRequests.Remove(id);
                    tcs.SetResult(root.TryGetProperty("payload", out var p) ? p.Clone() : root.Clone());
                }
            }
        }
        catch (Exception ex)
        {
            Log($"  [ERROR] Failed to process message: {ex.Message}");
        }
    }
}
