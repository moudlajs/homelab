using System.Net.Http.Json;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Update;

/// <summary>
/// Implementation of GitHub Release service for checking updates.
/// Supports authentication for private repositories via personal access token.
/// </summary>
public class GitHubReleaseService : IGitHubReleaseService
{
    private readonly HttpClient _httpClient;
    private readonly IHomelabConfigService _configService;
    private const string GitHubApiBase = "https://api.github.com";
    private const string RepoOwner = "moudlajs";
    private const string RepoName = "homelab";

    public GitHubReleaseService(HttpClient httpClient, IHomelabConfigService configService)
    {
        _httpClient = httpClient;
        _configService = configService;

        // GitHub API requires User-Agent header
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HomeLab-CLI");
        }

        // Add authorization header if token is configured (required for private repos)
        var token = _configService.GetGitHubToken();
        if (!string.IsNullOrEmpty(token) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        }
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync()
    {
        try
        {
            var url = $"{GitHubApiBase}/repos/{RepoOwner}/{RepoName}/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);
            return release;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<GitHubRelease?> GetReleaseByTagAsync(string tag)
    {
        try
        {
            // Ensure tag starts with 'v'
            if (!tag.StartsWith('v'))
            {
                tag = $"v{tag}";
            }

            var url = $"{GitHubApiBase}/repos/{RepoOwner}/{RepoName}/releases/tags/{tag}";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);
            return release;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> DownloadAssetAsync(GitHubAsset asset, string destinationPath)
    {
        try
        {
            // Download the file
            var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl);
            response.EnsureSuccessStatusCode();

            // Write to destination
            await using var fileStream = File.Create(destinationPath);
            await response.Content.CopyToAsync(fileStream);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public int CompareVersions(string v1, string v2)
    {
        // Remove 'v' prefix if present
        v1 = v1.TrimStart('v');
        v2 = v2.TrimStart('v');

        // Try to parse as Version objects
        if (Version.TryParse(v1, out var version1) && Version.TryParse(v2, out var version2))
        {
            return version1.CompareTo(version2);
        }

        // Fall back to string comparison
        return string.Compare(v1, v2, StringComparison.Ordinal);
    }
}
