using System.Reflection;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Display the current version of the HomeLab CLI.
/// </summary>
public class VersionCommand : Command<VersionCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version?.ToString() ?? "Unknown";

        var productName = assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product ?? "HomeLab CLI";

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            new Markup("[yellow]Product:[/]"),
            new Markup($"[cyan]{productName}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Version:[/]"),
            new Markup($"[green]{informationalVersion}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Runtime:[/]"),
            new Markup($"[dim].NET {Environment.Version}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Platform:[/]"),
            new Markup($"[dim]{Environment.OSVersion.Platform} ({RuntimeInformation.OSArchitecture})[/]")
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[green]Version Information[/]")
                .BorderColor(Color.Green)
                .RoundedBorder()
        );

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Run 'homelab self-update --check' to check for updates[/]");

        return 0;
    }
}
