namespace HomeLab.Cli.Models;

/// <summary>
/// Configuration for a smart TV.
/// </summary>
public class TvConfig
{
    public string Name { get; set; } = "TV";
    public string IpAddress { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string? ClientKey { get; set; }
    public TvType Type { get; set; } = TvType.LgWebOs;
}

/// <summary>
/// Type of smart TV.
/// </summary>
public enum TvType
{
    LgWebOs,
    Samsung,
    Sony,
    Generic
}
