namespace HomeLab.Cli.Services.Update;

/// <summary>
/// Service for interacting with GitHub Releases API to check for updates.
/// </summary>
public interface IGitHubReleaseService
{
    /// <summary>
    /// Get the latest release from GitHub.
    /// </summary>
    Task<GitHubRelease?> GetLatestReleaseAsync();

    /// <summary>
    /// Get a specific release by tag name (e.g., "v1.6.0").
    /// </summary>
    Task<GitHubRelease?> GetReleaseByTagAsync(string tag);

    /// <summary>
    /// Download a release asset (binary file) to the specified path.
    /// </summary>
    Task<bool> DownloadAssetAsync(GitHubAsset asset, string destinationPath);

    /// <summary>
    /// Compare two version strings. Returns:
    /// - Negative if v1 &lt; v2
    /// - Zero if v1 == v2
    /// - Positive if v1 &gt; v2
    /// </summary>
    int CompareVersions(string v1, string v2);
}
