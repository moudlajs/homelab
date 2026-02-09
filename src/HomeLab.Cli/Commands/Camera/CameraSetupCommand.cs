using System.ComponentModel;
using System.Text.Json;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraSetupCommand : AsyncCommand<CameraSetupCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings
    {
        [CommandOption("--url <URL>")]
        [Description("Scrypted URL (e.g. http://localhost:11080)")]
        public string? Url { get; set; }

        [CommandOption("--username <USER>")]
        [Description("Scrypted username")]
        public string? Username { get; set; }

        [CommandOption("--password <PASS>")]
        [Description("Scrypted password")]
        public string? Password { get; set; }

        [CommandOption("--token <TOKEN>")]
        [Description("Scrypted API token")]
        public string? Token { get; set; }
    }

    public CameraSetupCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[blue]Camera Setup Wizard[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var existing = await LoadConfigAsync();
        if (existing != null)
        {
            AnsiConsole.MarkupLine($"[dim]Existing config found: {existing.Url}[/]");
            AnsiConsole.WriteLine();
        }

        var defaultUrl = existing?.Url ?? "http://localhost:11080";
        var url = settings.Url ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Scrypted URL:").DefaultValue(defaultUrl));

        var defaultUser = existing?.Username ?? "";
        var username = settings.Username ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Username:").DefaultValue(defaultUser).AllowEmpty());

        var password = settings.Password ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Password:").DefaultValue("").AllowEmpty().Secret('*'));

        var token = settings.Token ?? AnsiConsole.Prompt(
            new TextPrompt<string>("API Token (optional):").DefaultValue("").AllowEmpty());

        // Test connectivity
        AnsiConsole.MarkupLine("\n[bold]Testing connectivity...[/]");
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync($"{url.TrimEnd('/')}/login");
            if (response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Scrypted is reachable!");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]![/] Scrypted responded with HTTP {(int)response.StatusCode}");
                if (!AnsiConsole.Confirm("Save config anyway?", true))
                {
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]![/] Cannot reach Scrypted: {ex.Message}");
            AnsiConsole.MarkupLine("[dim]Make sure Scrypted is running: docker compose up -d scrypted[/]");
            if (!AnsiConsole.Confirm("Save config anyway?", true))
            {
                return 1;
            }
        }

        var config = new CameraConfig
        {
            Url = url.TrimEnd('/'),
            Username = string.IsNullOrEmpty(username) ? null : username,
            Password = string.IsNullOrEmpty(password) ? null : password,
            Token = string.IsNullOrEmpty(token) ? null : token
        };

        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "camera.json");
        await File.WriteAllTextAsync(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.MarkupLine("\n[green]Configuration saved![/]");
        AnsiConsole.MarkupLine("[dim]Also add scrypted settings to config/homelab-cli.yaml for CLI integration.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Commands: homelab camera list | homelab camera status | homelab camera stream <id>[/]");

        return 0;
    }

    private static async Task<CameraConfig?> LoadConfigAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "camera.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CameraConfig>(await File.ReadAllTextAsync(path));
        }
        catch { return null; }
    }

    private class CameraConfig
    {
        public string Url { get; set; } = "http://localhost:11080";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Token { get; set; }
    }
}
