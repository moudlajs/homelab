using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Vpn;

/// <summary>
/// Interactive wizard to set up WireGuard VPN server configuration.
/// </summary>
public class VpnSetupCommand : AsyncCommand<VpnSetupCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IDockerService _dockerService;
    private const string ContainerName = "homelab_wireguard";

    public class Settings : CommandSettings
    {
        [CommandOption("--endpoint <ENDPOINT>")]
        [Description("Server endpoint (e.g., vpn.example.com or public IP)")]
        public string? Endpoint { get; set; }

        [CommandOption("--port <PORT>")]
        [Description("Server port (default: 51820)")]
        [DefaultValue(51820)]
        public int Port { get; set; } = 51820;

        [CommandOption("--non-interactive")]
        [Description("Skip interactive prompts (requires --endpoint)")]
        public bool NonInteractive { get; set; }
    }

    public VpnSetupCommand(IServiceClientFactory clientFactory, IDockerService dockerService)
    {
        _clientFactory = clientFactory;
        _dockerService = dockerService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateWireGuardClient();

        AnsiConsole.Write(new Rule("[blue]WireGuard VPN Setup Wizard[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Step 1: Check if already configured
        var existingConfig = await client.GetServerConfigAsync();
        if (existingConfig.IsConfigured)
        {
            AnsiConsole.MarkupLine("[yellow]VPN is already configured:[/]");
            AnsiConsole.MarkupLine($"  Endpoint: [cyan]{existingConfig.ServerEndpoint}:{existingConfig.ServerPort}[/]");
            AnsiConsole.MarkupLine($"  Public Key: [dim]{existingConfig.ServerPublicKey?[..20]}...[/]");
            AnsiConsole.WriteLine();

            if (!settings.NonInteractive)
            {
                var reconfigure = AnsiConsole.Confirm("Do you want to reconfigure?", false);
                if (!reconfigure)
                {
                    AnsiConsole.MarkupLine("[green]Setup cancelled. Existing configuration preserved.[/]");
                    return 0;
                }
            }
        }

        // Step 2: Check WireGuard container
        AnsiConsole.MarkupLine("[bold]Step 1:[/] Checking WireGuard container...");

        var containerExists = await _dockerService.ContainerExistsAsync(ContainerName);
        var containerRunning = containerExists && await _dockerService.IsContainerRunningAsync(ContainerName);

        if (!containerExists)
        {
            AnsiConsole.MarkupLine("[red]WireGuard container not found![/]");
            AnsiConsole.MarkupLine("[dim]Run 'docker-compose up -d wireguard' to start the container first.[/]");
            return 1;
        }

        if (!containerRunning)
        {
            AnsiConsole.MarkupLine($"[yellow]Container '{ContainerName}' exists but is not running.[/]");

            if (!settings.NonInteractive)
            {
                var startContainer = AnsiConsole.Confirm("Would you like to start it now?", true);
                if (startContainer)
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .StartAsync("Starting WireGuard container...", async ctx =>
                        {
                            await _dockerService.StartContainerAsync(ContainerName);
                            await Task.Delay(3000); // Wait for container to initialize
                        });

                    containerRunning = await _dockerService.IsContainerRunningAsync(ContainerName);
                    if (!containerRunning)
                    {
                        AnsiConsole.MarkupLine("[red]Failed to start container. Check docker logs for details.[/]");
                        return 1;
                    }
                    AnsiConsole.MarkupLine("[green]Container started successfully![/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Container must be running to complete setup.[/]");
                    return 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Container must be running. Use --non-interactive only when container is already running.[/]");
                return 1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Container '{ContainerName}' is running.[/]");
        }

        // Step 3: Get server public key from container
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 2:[/] Reading server public key...");

        string? serverPublicKey = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Reading server public key from container...", async ctx =>
            {
                serverPublicKey = await client.GetServerPublicKeyFromContainerAsync();
            });

        if (string.IsNullOrEmpty(serverPublicKey))
        {
            AnsiConsole.MarkupLine("[red]Could not read server public key from container.[/]");
            AnsiConsole.MarkupLine("[dim]The container may still be initializing. Wait a moment and try again.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Server public key found:[/] [dim]{serverPublicKey[..Math.Min(20, serverPublicKey.Length)]}...[/]");

        // Step 4: Get server endpoint
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3:[/] Configure server endpoint...");

        var endpoint = settings.Endpoint;
        if (string.IsNullOrEmpty(endpoint) && !settings.NonInteractive)
        {
            AnsiConsole.MarkupLine("[dim]This should be your public IP or a domain name (e.g., vpn.yoursite.com)[/]");

            // Try to detect public IP
            var detectedIp = await TryGetPublicIpAsync();
            if (!string.IsNullOrEmpty(detectedIp))
            {
                AnsiConsole.MarkupLine($"[dim]Detected public IP: {detectedIp}[/]");
            }

            endpoint = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter server endpoint (IP or domain):")
                    .DefaultValue(detectedIp ?? "")
                    .Validate(e => !string.IsNullOrWhiteSpace(e)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Endpoint cannot be empty")));
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            AnsiConsole.MarkupLine("[red]Server endpoint is required.[/]");
            return 1;
        }

        var port = settings.Port;
        if (!settings.NonInteractive)
        {
            port = AnsiConsole.Prompt(
                new TextPrompt<int>("Enter server port:")
                    .DefaultValue(51820)
                    .Validate(p => p > 0 && p < 65536
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Port must be between 1 and 65535")));
        }

        // Step 5: Save configuration
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 4:[/] Saving configuration...");

        var config = new VpnServerConfig
        {
            ServerPublicKey = serverPublicKey,
            ServerEndpoint = endpoint,
            ServerPort = port,
            AllowedIPs = "0.0.0.0/0",
            DNS = "10.8.0.1",
            Subnet = "10.8.0.0/24"
        };

        await client.UpdateServerConfigAsync(config);

        AnsiConsole.MarkupLine("[green]Configuration saved![/]");

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Setup Complete[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Endpoint", $"{endpoint}:{port}");
        table.AddRow("Server Public Key", $"{serverPublicKey[..20]}...");
        table.AddRow("Allowed IPs", config.AllowedIPs);
        table.AddRow("DNS", config.DNS);

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]You can now add peers with:[/] [cyan]homelab vpn add-peer <name>[/]");

        return 0;
    }

    private static async Task<string?> TryGetPublicIpAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetStringAsync("https://api.ipify.org");
            return response.Trim();
        }
        catch
        {
            return null;
        }
    }
}
