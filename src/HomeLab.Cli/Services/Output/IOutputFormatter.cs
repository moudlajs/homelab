namespace HomeLab.Cli.Services.Output;

/// <summary>
/// Supported output formats for export commands.
/// </summary>
public enum OutputFormat
{
    /// <summary>Table format (default, human-readable)</summary>
    Table,

    /// <summary>JSON format for automation</summary>
    Json,

    /// <summary>CSV format for spreadsheets</summary>
    Csv,

    /// <summary>YAML format for configuration</summary>
    Yaml
}

/// <summary>
/// Service for formatting output in different formats (JSON, CSV, YAML, Table).
/// Used by commands with --output flag support.
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Serialize an object to the specified format.
    /// </summary>
    /// <param name="data">The data to serialize</param>
    /// <param name="format">The output format</param>
    /// <returns>Formatted string ready for console output or file export</returns>
    string Format<T>(T data, OutputFormat format);

    /// <summary>
    /// Serialize a collection to the specified format.
    /// For CSV, this will include headers.
    /// </summary>
    string FormatCollection<T>(IEnumerable<T> data, OutputFormat format);

    /// <summary>
    /// Save formatted data to a file.
    /// </summary>
    Task SaveToFileAsync<T>(T data, OutputFormat format, string filePath);
}
