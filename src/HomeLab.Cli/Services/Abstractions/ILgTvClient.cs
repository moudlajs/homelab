using System.Text.Json;

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

    /// <summary>
    /// Turns the TV screen off (TV stays running).
    /// </summary>
    Task TurnScreenOffAsync();

    /// <summary>
    /// Turns the TV screen back on.
    /// </summary>
    Task TurnScreenOnAsync();

    /// <summary>
    /// Gets detailed power state information.
    /// </summary>
    Task<JsonElement> GetPowerStateAsync();

    /// <summary>
    /// Gets the current sound output device.
    /// </summary>
    Task<JsonElement> GetSoundOutputAsync();

    /// <summary>
    /// Changes the sound output device.
    /// </summary>
    Task ChangeSoundOutputAsync(string output);

    /// <summary>
    /// Gets the list of external input sources (HDMI, etc.).
    /// </summary>
    Task<JsonElement> GetExternalInputListAsync();

    /// <summary>
    /// Switches to a specific input source.
    /// </summary>
    Task SwitchInputAsync(string inputId);

    /// <summary>
    /// Gets the list of available TV channels.
    /// </summary>
    Task<JsonElement> GetChannelListAsync();

    /// <summary>
    /// Gets the currently tuned channel.
    /// </summary>
    Task<JsonElement> GetCurrentChannelAsync();

    /// <summary>
    /// Tunes to a specific channel.
    /// </summary>
    Task OpenChannelAsync(string channelId);

    /// <summary>
    /// Sends a toast notification to the TV screen.
    /// </summary>
    Task CreateToastAsync(string message);

    /// <summary>
    /// Gets system information (model, serial, etc.).
    /// </summary>
    Task<JsonElement> GetSystemInfoAsync();

    /// <summary>
    /// Gets software/firmware version information.
    /// </summary>
    Task<JsonElement> GetSoftwareInfoAsync();

    /// <summary>
    /// Gets system settings for a category.
    /// </summary>
    Task<JsonElement> GetSystemSettingsAsync(string category, string[] keys);

    /// <summary>
    /// Sets system settings for a category.
    /// </summary>
    Task SetSystemSettingsAsync(string category, Dictionary<string, object> settings);

    /// <summary>
    /// Closes an app by ID.
    /// </summary>
    Task CloseAppAsync(string appId);
}

/// <summary>
/// Represents an app installed on the TV.
/// </summary>
public class TvApp
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
