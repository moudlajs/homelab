using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Telegram.Bot;

namespace HomeLab.Cli.Commands.Bot;

public class BotSetupCommand : AsyncCommand<BotSetupCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[blue]Telegram Bot Setup[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Step 1: Get bot token
        AnsiConsole.MarkupLine("[yellow]Step 1:[/] Create a bot via Telegram @BotFather");
        AnsiConsole.MarkupLine("[dim]  1. Open Telegram and search for @BotFather[/]");
        AnsiConsole.MarkupLine("[dim]  2. Send /newbot and follow the prompts[/]");
        AnsiConsole.MarkupLine("[dim]  3. Copy the bot token[/]");
        AnsiConsole.WriteLine();

        var token = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter bot token:")
                .Secret());

        // Validate token
        AnsiConsole.WriteLine();
        string botUsername;
        try
        {
            var client = new TelegramBotClient(token);
            var me = await client.GetMe(cancellationToken);
            botUsername = me.Username ?? "unknown";
            AnsiConsole.MarkupLine($"[green]Bot verified:[/] @{botUsername}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Invalid token: {ex.Message}[/]");
            return 1;
        }

        // Step 2: Get user ID
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Step 2:[/] Get your Telegram user ID");
        AnsiConsole.MarkupLine($"[dim]  1. Open Telegram and search for @{botUsername}[/]");
        AnsiConsole.MarkupLine("[dim]  2. Send /start to the bot[/]");
        AnsiConsole.MarkupLine("[dim]  3. Press Enter here when done[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.Prompt(new TextPrompt<string>("Press [green]Enter[/] when you've sent /start:")
            .AllowEmpty());

        // Poll for the /start message to get user ID
        long userId = 0;
        try
        {
            var client = new TelegramBotClient(token);
            var updates = await client.GetUpdates(offset: -1, limit: 10, timeout: 5, cancellationToken: cancellationToken);
            foreach (var update in updates)
            {
                if (update.Message?.Text == "/start" && update.Message.From != null)
                {
                    userId = update.Message.From.Id;
                    break;
                }
            }

            if (userId == 0 && updates.Length > 0)
            {
                // Take the last message sender as fallback
                var last = updates[^1];
                if (last.Message?.From != null)
                {
                    userId = last.Message.From.Id;
                }
            }
        }
        catch
        {
            // Fallback to manual entry
        }

        if (userId == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Could not auto-detect user ID.[/]");
            userId = AnsiConsole.Prompt(
                new TextPrompt<long>("Enter your Telegram user ID manually:"));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Detected user ID:[/] {userId}");
        }

        // Step 3: Save to config
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "homelab", "homelab-cli.yaml");

        if (!File.Exists(configPath))
        {
            AnsiConsole.MarkupLine($"[red]Config file not found: {configPath}[/]");
            AnsiConsole.MarkupLine("[dim]Create it manually and add:[/]");
            AnsiConsole.MarkupLine($"[dim]services:\\n  telegram:\\n    token: {token}\\n    username: \"{userId}\"\\n    enabled: true[/]");
            return 1;
        }

        var yaml = await File.ReadAllTextAsync(configPath, cancellationToken);

        // Check if telegram section already exists
        if (yaml.Contains("telegram:"))
        {
            AnsiConsole.MarkupLine("[yellow]Telegram config already exists in config file.[/]");
            AnsiConsole.MarkupLine("[dim]Updating...[/]");
            // Simple replacement — find and replace the telegram block
            // This is crude but works for our YAML structure
            var lines = yaml.Split('\n').ToList();
            var telegramIdx = lines.FindIndex(l => l.Trim() == "telegram:");
            if (telegramIdx >= 0)
            {
                // Remove existing telegram block (up to next non-indented line)
                var removeCount = 1;
                for (var i = telegramIdx + 1; i < lines.Count; i++)
                {
                    if (lines[i].Length > 0 && !lines[i].StartsWith("    ") && !string.IsNullOrWhiteSpace(lines[i]))
                    {
                        break;
                    }

                    removeCount++;
                }
                lines.RemoveRange(telegramIdx, removeCount);
                lines.Insert(telegramIdx, $"  telegram:");
                lines.Insert(telegramIdx + 1, $"    token: {token}");
                lines.Insert(telegramIdx + 2, $"    username: \"{userId}\"");
                lines.Insert(telegramIdx + 3, $"    enabled: true");
                yaml = string.Join('\n', lines);
            }
        }
        else
        {
            // Append telegram section under services
            var servicesIdx = yaml.IndexOf("services:", StringComparison.Ordinal);
            if (servicesIdx >= 0)
            {
                // Find the end of services block and insert before next top-level key
                var insertText = $"  telegram:\n    token: {token}\n    username: \"{userId}\"\n    enabled: true\n";
                // Insert after the last service entry (before 'remote:' or end)
                var remoteIdx = yaml.IndexOf("\nremote:", StringComparison.Ordinal);
                if (remoteIdx >= 0)
                {
                    yaml = yaml.Insert(remoteIdx, "\n" + insertText);
                }
                else
                {
                    yaml += "\n" + insertText;
                }
            }
        }

        await File.WriteAllTextAsync(configPath, yaml, cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Setup complete![/]");
        AnsiConsole.MarkupLine($"  Bot: @{botUsername}");
        AnsiConsole.MarkupLine($"  User ID: {userId}");
        AnsiConsole.MarkupLine($"  Config: {configPath}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Start the bot with:[/] [cyan]homelab bot start[/]");
        AnsiConsole.MarkupLine("[dim]Run as daemon with:[/] [cyan]homelab bot schedule install[/]");

        return 0;
    }
}
