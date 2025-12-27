namespace HomeLab.Cli.Models;

/// <summary>
/// DNS statistics from AdGuard Home.
/// </summary>
public class DnsStats
{
    public long TotalQueries { get; set; }
    public long BlockedQueries { get; set; }
    public double BlockedPercentage { get; set; }
    public long SafeBrowsingBlocks { get; set; }
    public long ParentalBlocks { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Information about a blocked domain.
/// </summary>
public class BlockedDomain
{
    public string Domain { get; set; } = string.Empty;
    public long Count { get; set; }
}
