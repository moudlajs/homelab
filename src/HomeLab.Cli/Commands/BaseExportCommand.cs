using System.ComponentModel;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Base class for commands that support export functionality.
/// Provides common --output and --export flags.
/// </summary>
public abstract class BaseExportCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : BaseExportCommand<TSettings>.ExportSettings
{
    protected readonly IOutputFormatter OutputFormatter;

    protected BaseExportCommand(IOutputFormatter outputFormatter)
    {
        OutputFormatter = outputFormatter;
    }

    public class ExportSettings : CommandSettings
    {
        [CommandOption("-o|--output <FORMAT>")]
        [Description("Output format: table (default), json, csv, yaml")]
        public string? Output { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file instead of stdout")]
        public string? ExportFile { get; set; }
    }

    /// <summary>
    /// Helper to export data if output format is specified, otherwise returns false.
    /// </summary>
    protected async Task<bool> TryExportAsync<T>(TSettings settings, T data, string? errorMessage = null)
    {
        if (string.IsNullOrEmpty(settings.Output))
        {
            return false;
        }

        // Parse output format
        if (!Enum.TryParse<OutputFormat>(settings.Output, true, out var format))
        {
            AnsiConsole.MarkupLine($"[red]Invalid output format: {settings.Output}[/]");
            AnsiConsole.MarkupLine("[yellow]Valid formats: table, json, csv, yaml[/]");
            return true; // Exit early (handled export, even if error)
        }

        if (errorMessage != null)
        {
            // If there's an error, output it in the requested format
            var errorData = new { error = true, message = errorMessage };
            var formatted = OutputFormatter.Format(errorData, format);

            if (!string.IsNullOrEmpty(settings.ExportFile))
            {
                await File.WriteAllTextAsync(settings.ExportFile, formatted);
                AnsiConsole.MarkupLine($"[yellow]⚠ Exported error to {settings.ExportFile}[/]");
            }
            else
            {
                Console.WriteLine(formatted);
            }

            return true;
        }

        // Normal export
        var output = data is System.Collections.IEnumerable enumerable && !(data is string)
            ? OutputFormatter.FormatCollection(enumerable.Cast<object>(), format)
            : OutputFormatter.Format(data, format);

        if (!string.IsNullOrEmpty(settings.ExportFile))
        {
            await File.WriteAllTextAsync(settings.ExportFile, output);
            AnsiConsole.MarkupLine($"[green]✓ Exported to {settings.ExportFile}[/]");
        }
        else
        {
            Console.WriteLine(output);
        }

        return true;
    }
}
