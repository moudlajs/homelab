namespace HomeLab.Cli.Services.Output;

/// <summary>
/// Utility class for consistent byte formatting across the application.
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 GB").
    /// </summary>
    /// <param name="bytes">Number of bytes</param>
    /// <param name="decimalPlaces">Number of decimal places (default: 2)</param>
    /// <returns>Formatted string with appropriate unit</returns>
    public static string Format(long bytes, int decimalPlaces = 2)
    {
        if (bytes < 0)
        {
            return $"-{Format(-bytes, decimalPlaces)}";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString($"F{decimalPlaces}")} {SizeUnits[unitIndex]}";
    }

    /// <summary>
    /// Formats a byte count into a human-readable string, trimming trailing zeros.
    /// </summary>
    /// <param name="bytes">Number of bytes</param>
    /// <returns>Formatted string with appropriate unit (e.g., "1.5 GB" or "2 MB")</returns>
    public static string FormatCompact(long bytes)
    {
        if (bytes < 0)
        {
            return $"-{FormatCompact(-bytes)}";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        // Use G format to trim trailing zeros
        return $"{size:0.##} {SizeUnits[unitIndex]}";
    }

    /// <summary>
    /// Formats a byte rate into a human-readable string (e.g., "1.5 MB/s").
    /// </summary>
    /// <param name="bytesPerSecond">Bytes per second</param>
    /// <param name="decimalPlaces">Number of decimal places (default: 2)</param>
    /// <returns>Formatted string with appropriate unit and /s suffix</returns>
    public static string FormatRate(long bytesPerSecond, int decimalPlaces = 2)
    {
        return $"{Format(bytesPerSecond, decimalPlaces)}/s";
    }

    /// <summary>
    /// Formats a byte rate compactly (e.g., "1.5 MB/s" or "2 GB/s").
    /// </summary>
    /// <param name="bytesPerSecond">Bytes per second</param>
    /// <returns>Formatted string with appropriate unit and /s suffix</returns>
    public static string FormatRateCompact(long bytesPerSecond)
    {
        return $"{FormatCompact(bytesPerSecond)}/s";
    }

    /// <summary>
    /// Parses a string byte value and formats it.
    /// Handles cases where the value might already be formatted or is a raw number.
    /// </summary>
    /// <param name="value">String representation of bytes</param>
    /// <returns>Formatted string, or original value if parsing fails</returns>
    public static string ParseAndFormat(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0 B";
        }

        if (long.TryParse(value, out var bytes))
        {
            return FormatCompact(bytes);
        }

        // Return original if already formatted or unparseable
        return value;
    }
}
