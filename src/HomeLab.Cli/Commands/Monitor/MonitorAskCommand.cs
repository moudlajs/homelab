using System.ComponentModel;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Ask an AI question about your homelab state.
/// Collects current data and sends it with your question to the LLM.
/// </summary>
public class MonitorAskCommand : AsyncCommand<MonitorAskCommand.Settings>
{
    private readonly ILlmService _llmService;
    private readonly ISystemDataCollector _dataCollector;
    private readonly IEventLogService _eventLogService;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<question>")]
        [Description("Natural language question about your homelab")]
        public string Question { get; set; } = string.Empty;
    }

    public MonitorAskCommand(ILlmService llmService, ISystemDataCollector dataCollector, IEventLogService eventLogService)
    {
        _llmService = llmService;
        _dataCollector = dataCollector;
        _eventLogService = eventLogService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Check LLM availability first
        if (!await _llmService.IsAvailableAsync())
        {
            AnsiConsole.MarkupLine("[red]AI is not configured[/]");
            AnsiConsole.MarkupLine("[dim]Add services.ai.token to ~/.config/homelab/homelab-cli.yaml[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[cyan]Question:[/] {Markup.Escape(settings.Question)}\n");

        // Collect data + event history in parallel
        string prompt = string.Empty;

        await AnsiConsole.Status()
            .StartAsync("Collecting homelab data...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                var snapshotTask = _dataCollector.CollectAsync();
                var eventsTask = _eventLogService.ReadEventsAsync(since: DateTime.UtcNow.AddHours(-24));

                await Task.WhenAll(snapshotTask, eventsTask);

                var snapshot = await snapshotTask;
                var events = await eventsTask;
                prompt = $"User question: {settings.Question}\n\n{_dataCollector.FormatAsPrompt(snapshot, events.Count > 0 ? events : null)}";
            });

        // Send to AI
        var systemPrompt = "You are a homelab monitoring assistant. Answer the user's question based on the system data provided. " +
            "Be concise and specific. If the data doesn't contain enough information to answer fully, say so. " +
            "Use plain text, no markdown.";

        LlmResponse? response = null;

        await AnsiConsole.Status()
            .StartAsync($"Thinking ({_llmService.ProviderName})...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                response = await _llmService.SendMessageAsync(systemPrompt, prompt, 512);
            });

        if (response == null || !response.Success)
        {
            AnsiConsole.MarkupLine($"[red]AI request failed: {response?.Error ?? "Unknown error"}[/]");
            return 1;
        }

        // Render response
        var panel = new Panel(Markup.Escape(response.Content))
            .Header("[cyan]Answer[/]")
            .BorderColor(Color.Cyan)
            .RoundedBorder()
            .Padding(1, 1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        const decimal inputPricePerMillion = 1.00m;
        const decimal outputPricePerMillion = 5.00m;
        var cost = (response.InputTokens * inputPricePerMillion / 1_000_000m)
                 + (response.OutputTokens * outputPricePerMillion / 1_000_000m);
        AnsiConsole.MarkupLine($"[dim]Tokens: {response.InputTokens} in, {response.OutputTokens} out (~${cost:F4})[/]");

        return 0;
    }
}
