using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Vpn;

/// <summary>
/// Adds a new VPN peer and generates configuration.
/// </summary>
public class VpnAddPeerCommand : AsyncCommand<VpnAddPeerCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public VpnAddPeerCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name for the VPN peer (e.g., 'danny-phone')")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("--qr")]
        [Description("Display QR code for mobile devices")]
        [DefaultValue(true)]
        public bool ShowQrCode { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[cyan]Adding VPN peer:[/] {settings.Name}\n");

        var client = _clientFactory.CreateWireGuardClient();

        // Add the peer
        string peerConfig;
        try
        {
            peerConfig = await AnsiConsole.Status()
                .StartAsync("Generating peer configuration...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await client.AddPeerAsync(settings.Name);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to add peer: {ex.Message}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Peer '{settings.Name}' added successfully!\n");

        // Display configuration (use Markup.Escape to prevent interpretation of [Interface] etc.)
        var configPanel = new Panel(Markup.Escape(peerConfig))
            .Header($"[yellow]Configuration for {settings.Name}[/]")
            .BorderColor(Color.Green)
            .RoundedBorder();

        AnsiConsole.Write(configPanel);
        AnsiConsole.WriteLine();

        // Generate and display QR code
        if (settings.ShowQrCode)
        {
            try
            {
                AnsiConsole.MarkupLine("[cyan]Generating QR code...[/]\n");

                var qrCodeBytes = await client.GenerateQRCodeAsync(peerConfig);

                // Save QR code to temp file
                var tempPath = Path.Combine(Path.GetTempPath(), $"wireguard-{settings.Name}-qr.png");
                await File.WriteAllBytesAsync(tempPath, qrCodeBytes, cancellationToken);

                AnsiConsole.MarkupLine($"[green]✓[/] QR code saved to: [cyan]{tempPath}[/]");
                AnsiConsole.MarkupLine($"\n[dim]Scan this QR code with the WireGuard mobile app to import the configuration.[/]");

                // Try to open the QR code (macOS)
                try
                {
                    var process = System.Diagnostics.Process.Start("open", tempPath);
                    if (process != null)
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Opening QR code...");
                    }
                }
                catch
                {
                    // Silently fail if can't open
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Could not generate QR code: {ex.Message}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Next steps:[/]");
        AnsiConsole.MarkupLine("  1. Copy the configuration above or scan the QR code");
        AnsiConsole.MarkupLine("  2. Import it into your WireGuard client");
        AnsiConsole.MarkupLine($"  3. Check status with: [cyan]homelab vpn status[/]");

        return 0;
    }
}
