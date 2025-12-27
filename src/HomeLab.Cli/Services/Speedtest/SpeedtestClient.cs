using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Speedtest;

/// <summary>
/// Client for interacting with Speedtest Tracker API.
/// Speedtest Tracker is a self-hosted internet speed testing dashboard.
/// </summary>
public class SpeedtestClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public SpeedtestClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Get health check for Speedtest Tracker service.
    /// </summary>
    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/health");

            return response.IsSuccessStatusCode
                ? new ServiceHealthInfo { IsHealthy = true, Message = "Speedtest Tracker is running" }
                : new ServiceHealthInfo { IsHealthy = false, Message = $"HTTP {(int)response.StatusCode}" };
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
    /// Get latest speedtest results.
    /// </summary>
    public async Task<List<SpeedtestResult>> GetRecentResultsAsync(int limit = 10)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<SpeedtestResult>>(
                $"{_baseUrl}/api/speedtest/results?limit={limit}");

            return response ?? new List<SpeedtestResult>();
        }
        catch
        {
            return GetMockResults();
        }
    }

    /// <summary>
    /// Get speedtest statistics (averages, trends).
    /// </summary>
    public async Task<SpeedtestStats> GetStatsAsync(int days = 30)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<SpeedtestStats>(
                $"{_baseUrl}/api/speedtest/stats?days={days}");

            return response ?? GetMockStats();
        }
        catch
        {
            return GetMockStats();
        }
    }

    /// <summary>
    /// Trigger a new speedtest.
    /// </summary>
    public async Task<bool> RunSpeedtestAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/speedtest/run", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Mock data for testing
    private List<SpeedtestResult> GetMockResults()
    {
        var random = new Random();
        var results = new List<SpeedtestResult>();

        for (int i = 0; i < 10; i++)
        {
            results.Add(new SpeedtestResult
            {
                Id = i + 1,
                Timestamp = DateTime.UtcNow.AddHours(-i * 6),
                DownloadSpeed = 450 + random.Next(-50, 50),
                UploadSpeed = 45 + random.Next(-10, 10),
                Ping = 15 + random.Next(-5, 10),
                Server = i % 2 == 0 ? "Cloudflare" : "Google",
                Isp = "Example ISP"
            });
        }

        return results;
    }

    private SpeedtestStats GetMockStats()
    {
        return new SpeedtestStats
        {
            AvgDownload = 455.5m,
            AvgUpload = 47.2m,
            AvgPing = 18.5m,
            MinDownload = 380.5m,
            MaxDownload = 510.2m,
            TotalTests = 240,
            Period = 30
        };
    }
}

/// <summary>
/// Represents a single speedtest result.
/// </summary>
public class SpeedtestResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("download")]
    public decimal DownloadSpeed { get; set; } // Mbps

    [JsonPropertyName("upload")]
    public decimal UploadSpeed { get; set; } // Mbps

    [JsonPropertyName("ping")]
    public decimal Ping { get; set; } // ms

    [JsonPropertyName("server")]
    public string Server { get; set; } = string.Empty;

    [JsonPropertyName("isp")]
    public string Isp { get; set; } = string.Empty;
}

/// <summary>
/// Speedtest statistics over a period.
/// </summary>
public class SpeedtestStats
{
    [JsonPropertyName("avg_download")]
    public decimal AvgDownload { get; set; }

    [JsonPropertyName("avg_upload")]
    public decimal AvgUpload { get; set; }

    [JsonPropertyName("avg_ping")]
    public decimal AvgPing { get; set; }

    [JsonPropertyName("min_download")]
    public decimal MinDownload { get; set; }

    [JsonPropertyName("max_download")]
    public decimal MaxDownload { get; set; }

    [JsonPropertyName("total_tests")]
    public int TotalTests { get; set; }

    [JsonPropertyName("period_days")]
    public int Period { get; set; }
}
