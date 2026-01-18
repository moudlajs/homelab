namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Interface for LG WebOS TV control.
/// </summary>
public interface ILgTvClient
{
    /// <summary>
    /// Connects to the TV and authenticates.
    /// </summary>
    /// <param name="ipAddress">TV IP address</param>
    /// <param name="clientKey">Previously saved client key (null for first-time pairing)</param>
    /// <returns>Client key to save for future connections</returns>
    Task<string?> ConnectAsync(string ipAddress, string? clientKey = null);

    /// <summary>
    /// Disconnects from the TV.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Turns off the TV.
    /// </summary>
    Task PowerOffAsync();

    /// <summary>
    /// Gets the current TV power state.
    /// </summary>
    Task<bool> IsPoweredOnAsync(string ipAddress);

    /// <summary>
    /// Sets the TV volume.
    /// </summary>
    Task SetVolumeAsync(int volume);

    /// <summary>
    /// Gets the current volume.
    /// </summary>
    Task<int> GetVolumeAsync();

    /// <summary>
    /// Mutes or unmutes the TV.
    /// </summary>
    Task SetMuteAsync(bool mute);

    /// <summary>
    /// Opens an app on the TV (e.g., Netflix, YouTube).
    /// </summary>
    Task LaunchAppAsync(string appId);

    /// <summary>
    /// Gets the list of installed apps.
    /// </summary>
    Task<List<TvApp>> GetAppsAsync();
}

/// <summary>
/// Represents an app installed on the TV.
/// </summary>
public class TvApp
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
