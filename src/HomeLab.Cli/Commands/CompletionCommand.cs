using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Generates shell completion scripts for bash and zsh.
/// Usage: homelab completion bash > /usr/local/etc/bash_completion.d/homelab
///        homelab completion zsh > ~/.zsh/completions/_homelab
/// </summary>
public class CompletionCommand : Command<CompletionCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<shell>")]
        [Description("Shell type: bash or zsh")]
        public string Shell { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var shell = settings.Shell.ToLowerInvariant();

        switch (shell)
        {
            case "bash":
                Console.WriteLine(GenerateBashCompletion());
                break;
            case "zsh":
                Console.WriteLine(GenerateZshCompletion());
                break;
            default:
                AnsiConsole.MarkupLine("[red]Error: Unknown shell type. Use 'bash' or 'zsh'[/]");
                return 1;
        }

        return 0;
    }

    private string GenerateBashCompletion()
    {
        return @"# Bash completion for homelab CLI
# Install: homelab completion bash > /usr/local/etc/bash_completion.d/homelab
# Or: homelab completion bash > ~/.bash_completion.d/homelab

_homelab_completions()
{
    local cur prev words cword
    _init_completion || return

    # Top-level commands
    local commands=""status st service svc config logs image-update cleanup version self-update tui ui dashboard vpn dns monitor remote uptime speedtest ha traefik network quick-restart qr quick-update qu quick-backup qb quick-fix qf completion""

    # VPN subcommands
    local vpn_commands=""status ls list add-peer add remove-peer rm remove""

    # DNS subcommands
    local dns_commands=""stats st blocked bl""

    # Monitor subcommands
    local monitor_commands=""alerts al targets tg dashboard dash db""

    # Remote subcommands
    local remote_commands=""connect list status sync remove""

    # Uptime subcommands
    local uptime_commands=""status st ls alerts al add remove rm""

    # Speedtest subcommands
    local speedtest_commands=""run stats st""

    # Home Assistant subcommands
    local ha_commands=""status st ls control get list""

    # Traefik subcommands
    local traefik_commands=""status st routes services middlewares mw""

    # Network subcommands
    local network_commands=""scan ports devices traffic""

    # Common flags
    local output_flags=""--output --export""

    # Handle subcommands
    if [ $cword -eq 1 ]; then
        # Complete top-level commands
        COMPREPLY=( $(compgen -W ""$commands"" -- ""$cur"") )
    elif [ $cword -eq 2 ]; then
        # Complete subcommands based on previous word
        case ""$prev"" in
            vpn)
                COMPREPLY=( $(compgen -W ""$vpn_commands"" -- ""$cur"") )
                ;;
            dns)
                COMPREPLY=( $(compgen -W ""$dns_commands"" -- ""$cur"") )
                ;;
            monitor)
                COMPREPLY=( $(compgen -W ""$monitor_commands"" -- ""$cur"") )
                ;;
            remote)
                COMPREPLY=( $(compgen -W ""$remote_commands"" -- ""$cur"") )
                ;;
            uptime)
                COMPREPLY=( $(compgen -W ""$uptime_commands"" -- ""$cur"") )
                ;;
            speedtest)
                COMPREPLY=( $(compgen -W ""$speedtest_commands"" -- ""$cur"") )
                ;;
            ha)
                COMPREPLY=( $(compgen -W ""$ha_commands"" -- ""$cur"") )
                ;;
            traefik)
                COMPREPLY=( $(compgen -W ""$traefik_commands"" -- ""$cur"") )
                ;;
            network)
                COMPREPLY=( $(compgen -W ""$network_commands"" -- ""$cur"") )
                ;;
            completion)
                COMPREPLY=( $(compgen -W ""bash zsh"" -- ""$cur"") )
                ;;
        esac
    else
        # Complete flags
        case ""$cur"" in
            -*)
                COMPREPLY=( $(compgen -W ""$output_flags"" -- ""$cur"") )
                ;;
        esac
    fi
}

complete -F _homelab_completions homelab
";
    }

    private string GenerateZshCompletion()
    {
        return @"#compdef homelab
# Zsh completion for homelab CLI
# Install: homelab completion zsh > ~/.zsh/completions/_homelab
# Make sure ~/.zsh/completions is in your $fpath

_homelab() {
    local line state

    _arguments -C \
        ""1: :->cmds"" \
        ""*::arg:->args""

    case $state in
        cmds)
            _values ""homelab commands"" \
                ""status[Display homelab status dashboard]"" \
                ""st[Alias for status]"" \
                ""service[Manage service lifecycle]"" \
                ""svc[Alias for service]"" \
                ""config[Manage configuration]"" \
                ""logs[View container logs]"" \
                ""image-update[Update container images]"" \
                ""cleanup[Clean up unused Docker resources]"" \
                ""version[Display version information]"" \
                ""self-update[Update HomeLab CLI to latest version]"" \
                ""tui[Live dashboard (Terminal UI mode)]"" \
                ""ui[Alias for tui]"" \
                ""dashboard[Alias for tui]"" \
                ""vpn[Manage VPN peers and configuration]"" \
                ""dns[Manage DNS and ad-blocking]"" \
                ""monitor[Monitor homelab metrics and alerts]"" \
                ""remote[Manage remote homelab connections]"" \
                ""uptime[Monitor service uptime and availability]"" \
                ""speedtest[Monitor internet connection speed]"" \
                ""ha[Control Home Assistant smart home devices]"" \
                ""traefik[Manage Traefik reverse proxy]"" \
                ""network[Network scanning and monitoring]"" \
                ""quick-restart[Quick restart a service]"" \
                ""qr[Alias for quick-restart]"" \
                ""quick-update[Quick update service]"" \
                ""qu[Alias for quick-update]"" \
                ""quick-backup[Quick backup container configs]"" \
                ""qb[Alias for quick-backup]"" \
                ""quick-fix[Quick fix service]"" \
                ""qf[Alias for quick-fix]"" \
                ""completion[Generate shell completion scripts]""
            ;;
        args)
            case $line[1] in
                vpn)
                    _values ""vpn commands"" \
                        ""status[Display VPN peer status]"" \
                        ""ls[Alias for status]"" \
                        ""list[Alias for status]"" \
                        ""add-peer[Add a new VPN peer]"" \
                        ""add[Alias for add-peer]"" \
                        ""remove-peer[Remove a VPN peer]"" \
                        ""rm[Alias for remove-peer]"" \
                        ""remove[Alias for remove-peer]""
                    ;;
                dns)
                    _values ""dns commands"" \
                        ""stats[Display DNS statistics]"" \
                        ""st[Alias for stats]"" \
                        ""blocked[Display recently blocked domains]"" \
                        ""bl[Alias for blocked]""
                    ;;
                monitor)
                    _values ""monitor commands"" \
                        ""alerts[Display active Prometheus alerts]"" \
                        ""al[Alias for alerts]"" \
                        ""targets[Display Prometheus scrape targets]"" \
                        ""tg[Alias for targets]"" \
                        ""dashboard[Open Grafana dashboards]"" \
                        ""dash[Alias for dashboard]"" \
                        ""db[Alias for dashboard]""
                    ;;
                remote)
                    _values ""remote commands"" \
                        ""connect[Add or update a remote connection]"" \
                        ""list[List all configured remote connections]"" \
                        ""status[Check status of remote homelab]"" \
                        ""sync[Sync docker-compose files with remote]"" \
                        ""remove[Remove a remote connection]""
                    ;;
                uptime)
                    _values ""uptime commands"" \
                        ""status[Display uptime monitoring status]"" \
                        ""st[Alias for status]"" \
                        ""ls[Alias for status]"" \
                        ""alerts[Show recent uptime alerts and incidents]"" \
                        ""al[Alias for alerts]"" \
                        ""add[Add a new service to monitor]"" \
                        ""remove[Remove a monitor from tracking]"" \
                        ""rm[Alias for remove]""
                    ;;
                speedtest)
                    _values ""speedtest commands"" \
                        ""run[Run a new speed test]"" \
                        ""stats[Display speed test statistics and history]"" \
                        ""st[Alias for stats]""
                    ;;
                ha)
                    _values ""home assistant commands"" \
                        ""status[Display all Home Assistant entities]"" \
                        ""st[Alias for status]"" \
                        ""ls[Alias for status]"" \
                        ""control[Control devices (on, off, toggle)]"" \
                        ""get[Get details of a specific entity]"" \
                        ""list[List entities by domain]""
                    ;;
                traefik)
                    _values ""traefik commands"" \
                        ""status[Display Traefik overview and status]"" \
                        ""st[Alias for status]"" \
                        ""routes[List all HTTP routers]"" \
                        ""services[List all backend services]"" \
                        ""middlewares[List all middlewares]"" \
                        ""mw[Alias for middlewares]""
                    ;;
                network)
                    _values ""network commands"" \
                        ""scan[Discover devices on network]"" \
                        ""ports[Scan ports on devices]"" \
                        ""devices[List tracked network devices (ntopng)]"" \
                        ""traffic[Display network traffic statistics]""
                    ;;
                completion)
                    _values ""shell types"" \
                        ""bash[Generate bash completion script]"" \
                        ""zsh[Generate zsh completion script]""
                    ;;
            esac
            ;;
    esac
}

_homelab
";
    }
}
