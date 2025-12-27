using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for AdGuard Home DNS service operations.
/// </summary>
public interface IAdGuardClient : IServiceClient
{
    /// <summary>
    /// Gets DNS statistics (queries, blocks, etc.).
    /// </summary>
    Task<DnsStats> GetStatsAsync();

    /// <summary>
    /// Gets the top blocked domains.
    /// </summary>
    Task<List<BlockedDomain>> GetTopBlockedDomainsAsync(int limit = 10);

    /// <summary>
    /// Updates DNS filter lists.
    /// </summary>
    Task UpdateFiltersAsync();
}
