using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.HomeAssistant;

/// <summary>
/// Control Home Assistant devices (turn on/off, toggle).
/// </summary>
public class HaControlCommand : AsyncCommand<HaControlCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ACTION>")]
        [Description("Action: on, off, toggle")]
        public string Action { get; set; } = string.Empty;

        [CommandArgument(1, "<ENTITY_ID>")]
        [Description("Entity ID (e.g., light.living_room)")]
        public string EntityId { get; set; } = string.Empty;
    }

    public HaControlCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateHomeAssistantClient();

        var action = settings.Action.ToLower();
        var entityId = settings.EntityId;

        AnsiConsole.MarkupLine($"[yellow]⚡[/] {action.ToUpper()} [cyan]{entityId}[/]...\n");

        bool success;

        switch (action)
        {
            case "on":
            case "turn-on":
            case "turn_on":
                success = await client.TurnOnAsync(entityId);
                break;

            case "off":
            case "turn-off":
            case "turn_off":
                success = await client.TurnOffAsync(entityId);
                break;

            case "toggle":
                success = await client.ToggleAsync(entityId);
                break;

            default:
                AnsiConsole.MarkupLine($"[red]✗ Unknown action:[/] {settings.Action}");
                AnsiConsole.MarkupLine("[yellow]Valid actions: on, off, toggle[/]");
                return 1;
        }

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Successfully executed [cyan]{action}[/] on [cyan]{entityId}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to execute[/] [cyan]{action}[/] on [cyan]{entityId}[/]");
            return 1;
        }
    }
}
