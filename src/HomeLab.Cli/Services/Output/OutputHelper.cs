using Spectre.Console;

namespace HomeLab.Cli.Services.Output;

/// <summary>
/// Helper methods for quick export support in any command.
/// Provides a simple way to add --output and --export flags.
/// </summary>
public static class OutputHelper
{
    /// <summary>
    /// Export data if output format is specified, otherwise return false to continue with UI.
    /// </summary>
    public static async Task<bool> TryExportAsync<T>(
        IOutputFormatter formatter,
        string? outputFormat,
        string? exportFile,
        T data)
    {
        if (string.IsNullOrEmpty(outputFormat))
            return false;

        // Parse format
        if (!Enum.TryParse<OutputFormat>(outputFormat, true, out var format))
        {
            AnsiConsole.MarkupLine($"[red]Invalid output format: {outputFormat}[/]");
            AnsiConsole.MarkupLine("[yellow]Valid formats: table, json, csv, yaml[/]");
            return true; // Handled (even though error)
        }

        // Format data
        var output = data is System.Collections.IEnumerable enumerable && !(data is string)
            ? formatter.FormatCollection(enumerable.Cast<object>(), format)
            : formatter.Format(data, format);

        // Output to file or stdout
        if (!string.IsNullOrEmpty(exportFile))
        {
            await File.WriteAllTextAsync(exportFile, output);
            AnsiConsole.MarkupLine($"[green]✓ Exported to {exportFile}[/]");
        }
        else
        {
            Console.WriteLine(output);
        }

        return true; // Handled
    }
}
