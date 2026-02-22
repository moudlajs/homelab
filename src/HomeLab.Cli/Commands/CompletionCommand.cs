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
    local commands=""status st service svc config logs image-update cleanup version self-update tui ui dashboard vpn dns monitor remote uptime ha traefik network tv completion shell""

    # VPN subcommands
    local vpn_commands=""status st up down devices ls""

    # DNS subcommands
    local dns_commands=""stats st blocked bl""

    # Monitor subcommands
    local monitor_commands=""report ai ask collect history hist schedule sched""

    # Remote subcommands
    local remote_commands=""connect list status sync remove""

    # Uptime subcommands
    local uptime_commands=""status st ls alerts al add remove rm""

    # Home Assistant subcommands
    local ha_commands=""status st ls control get list""

    # Traefik subcommands
    local traefik_commands=""status st routes services middlewares mw""

    # Network subcommands
    local network_commands=""scan ports devices traffic intrusion alerts status st""

    # TV subcommands
    local tv_commands=""on off apps launch key screen input sound channel info notify settings screenshot wake sleep status st setup debug""

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
            ha)
                COMPREPLY=( $(compgen -W ""$ha_commands"" -- ""$cur"") )
                ;;
            traefik)
                COMPREPLY=( $(compgen -W ""$traefik_commands"" -- ""$cur"") )
                ;;
            network)
                COMPREPLY=( $(compgen -W ""$network_commands"" -- ""$cur"") )
                ;;
            tv)
                COMPREPLY=( $(compgen -W ""$tv_commands"" -- ""$cur"") )
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
                ""vpn[Manage VPN connection (Tailscale)]"" \
                ""dns[Manage DNS and ad-blocking]"" \
                ""monitor[Monitor homelab metrics and alerts]"" \
                ""remote[Manage remote homelab connections]"" \
                ""uptime[Monitor service uptime and availability]"" \
                ""ha[Control Home Assistant smart home devices]"" \
                ""traefik[Manage Traefik reverse proxy]"" \
                ""network[Network scanning and monitoring]"" \
                ""tv[Control LG WebOS Smart TV]"" \
                ""shell[Interactive shell mode]"" \
                ""completion[Generate shell completion scripts]""
            ;;
        args)
            case $line[1] in
                vpn)
                    _values ""vpn commands"" \
                        ""status[Display VPN connection status]"" \
                        ""st[Alias for status]"" \
                        ""up[Connect to VPN]"" \
                        ""down[Disconnect from VPN]"" \
                        ""devices[List all VPN devices]"" \
                        ""ls[Alias for devices]""
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
                        ""report[AI-powered homelab health summary]"" \
                        ""ai[Alias for report]"" \
                        ""ask[Ask AI about your homelab]"" \
                        ""collect[Collect and log event snapshot]"" \
                        ""history[Show event timeline with gap detection]"" \
                        ""hist[Alias for history]"" \
                        ""schedule[Manage periodic collection]"" \
                        ""sched[Alias for schedule]""
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
                        ""traffic[Display network traffic statistics]"" \
                        ""intrusion[Display security alerts (Suricata IDS)]"" \
                        ""alerts[Alias for intrusion]"" \
                        ""status[Comprehensive network health overview]"" \
                        ""st[Alias for status]""
                    ;;
                tv)
                    _values ""tv commands"" \
                        ""on[Turn TV on via Wake-on-LAN]"" \
                        ""off[Turn TV off via WebOS API]"" \
                        ""apps[List installed apps on TV]"" \
                        ""launch[Launch an app on TV]"" \
                        ""key[Send remote control key to TV]"" \
                        ""screen[Turn screen off/on without power cycle]"" \
                        ""input[List or switch input sources]"" \
                        ""sound[Get or change sound output]"" \
                        ""channel[List or tune to channel]"" \
                        ""info[Show system and software info]"" \
                        ""notify[Send toast notification]"" \
                        ""settings[Get or set system settings]"" \
                        ""screenshot[Capture screenshot of TV screen]"" \
                        ""ss[Alias for screenshot]"" \
                        ""wake[Wake TV from screensaver]"" \
                        ""sleep[Get or set sleep timer]"" \
                        ""status[Check TV status and connectivity]"" \
                        ""st[Alias for status]"" \
                        ""setup[Configure and pair with TV]"" \
                        ""debug[Debug TV connection]""
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
