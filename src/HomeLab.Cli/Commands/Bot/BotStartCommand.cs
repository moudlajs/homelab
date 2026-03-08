using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Bot;

public class BotStartCommand : AsyncCommand<BotStartCommand.Settings>
{
    private readonly ITelegramBotService _botService;

    public class Settings : CommandSettings { }

    public BotStartCommand(ITelegramBotService botService) => _botService = botService;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!_botService.IsConfigured())
        {
            AnsiConsole.MarkupLine("[red]Telegram bot not configured.[/]");
            AnsiConsole.MarkupLine("[dim]Run 'homelab bot setup' first.[/]");
            return 1;
        }

        try
        {
            await _botService.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown via Ctrl+C
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Bot crashed: {ex.Message}[/]");
            return 1;
        }

        return 0;
    }
}
