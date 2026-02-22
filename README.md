# HomeLab CLI

> Command-line tool for managing a Mac Mini M4 homelab. Docker, networking, VPN, TV, AI monitoring — all from the terminal.

[![CI](https://github.com/moudlajs/homelab/actions/workflows/ci.yml/badge.svg)](https://github.com/moudlajs/homelab/actions/workflows/ci.yml)
[![Release](https://github.com/moudlajs/homelab/actions/workflows/release.yml/badge.svg)](https://github.com/moudlajs/homelab/actions/workflows/release.yml)
[![CodeQL](https://github.com/moudlajs/homelab/actions/workflows/codeql.yml/badge.svg)](https://github.com/moudlajs/homelab/actions/workflows/codeql.yml)
[![Latest Release](https://img.shields.io/github/v/release/moudlajs/homelab)](https://github.com/moudlajs/homelab/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-macOS%20ARM64-lightgrey)](https://github.com/moudlajs/homelab)

## Install

```bash
# From GitHub release
curl -L https://github.com/moudlajs/homelab/releases/latest/download/homelab -o homelab
chmod +x homelab && mv homelab ~/.local/bin/

# Or build from source
dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true -o publish-single
cp publish-single/HomeLab.Cli ~/.local/bin/homelab
```

**Requirements:** macOS ARM64, Docker (OrbStack recommended). No .NET runtime needed (self-contained binary).

## Infrastructure

Docker services managed via `docker-compose.yml`:

| Service | Port | Description |
|---------|------|-------------|
| AdGuard Home | 3000, 53 | DNS ad-blocking (needs router config) |
| ntopng | 3002 | Network traffic monitoring |
| Suricata | — | Intrusion detection (IDS) on eth0 |
| Scrypted | 11080 | Camera/NVR management (WebUI) |
| Uptime Kuma | 3001 | Service uptime monitoring |

## Commands

Run `homelab` with no args for interactive shell with tab completion.

### System

```
homelab status                          Service health dashboard
homelab service start|stop|restart <s>  Container lifecycle
homelab config view|edit|backup|restore Configuration management
homelab logs <container>                Container logs
homelab image-update <image>            Pull latest image
homelab cleanup [--volumes]             Docker resource cleanup
```

### AI Monitoring

```
homelab monitor report [--raw]          AI health summary (Claude Haiku)
homelab monitor ask "<question>"        Ask AI about your homelab
homelab monitor collect                 Capture event snapshot
homelab monitor history [--last 24h]    Event timeline with change detection
homelab monitor schedule install|uninstall  Periodic collection (10min)
```

Collects system metrics, Docker state, network info, and Suricata alerts — sends to Claude Haiku for analysis. ~$0.002/query. Event snapshots stored locally with 7-day retention.

### VPN (Tailscale)

```
homelab vpn status                      Connection status
homelab vpn up                          Connect
homelab vpn down                        Disconnect
homelab vpn devices                     List tailnet devices
```

### Network Security

```
homelab network scan                    nmap device discovery
homelab network ports --device <ip>     Port scanning
homelab network devices                 ntopng tracked devices
homelab network traffic                 Traffic statistics
homelab network intrusion [--severity]  Suricata IDS alerts
homelab network status                  Overall health dashboard
homelab network analyze [--last] [--ai] Anomaly detection + AI analysis
```

Requires `nmap` (`brew install nmap`). ntopng and Suricata run as Docker containers. Anomaly detection: new devices, traffic spikes, IDS alerts.

### DNS (AdGuard Home)

```
homelab dns stats                       Query statistics
homelab dns blocked [-n 20]             Top blocked domains
```

### TV Control (LG WebOS)

```
homelab tv setup                        Pair with TV
homelab tv on [--app netflix]           Wake via WOL + launch app
homelab tv off                          Power off via WebOS API
homelab tv status                       Connection status
homelab tv apps                         List installed apps
homelab tv launch <app>                 Launch app
homelab tv key <key>                    Send remote control key
homelab tv screen                       Turn screen off/on (no power cycle)
homelab tv input                        List or switch HDMI inputs
homelab tv sound                        Get or change sound output
homelab tv channel                      List channels or tune
homelab tv info                         System info, software version
homelab tv notify "<message>"           Send toast notification to TV
homelab tv settings                     Get or set TV system settings
homelab tv debug                        Debug connection and app detection
```

See [docs/TV_CONTROL.md](docs/TV_CONTROL.md).

### Uptime Monitoring (Uptime Kuma)

```
homelab uptime status                   Service uptime dashboard
homelab uptime alerts                   Recent incidents
homelab uptime add <name> <url>         Add a new monitor
homelab uptime remove <id>              Remove a monitor
```

Connects to Uptime Kuma via socket.io API. Monitors configured: ntopng, Scrypted, AdGuard Home, Kuma self.

### Other

```
homelab ha status|control|get|list      Home Assistant
homelab traefik status|routes|...       Traefik reverse proxy
homelab remote connect|list|status|...  Remote SSH management
homelab dashboard                       Live terminal dashboard
homelab self-update [--check]           Update CLI binary
homelab version                         Version info
homelab completion bash|zsh             Shell completion scripts
```

## Configuration

Config file at `~/.config/homelab/homelab-cli.yaml` (falls back to `./config/homelab-cli.yaml`):

```yaml
services:
  adguard:
    url: "http://localhost:3000"
    username: "admin"
    password: "admin"
    enabled: true
  ai:
    provider: "anthropic"
    model: "claude-haiku-4-5-20251001"
    token: ""  # https://console.anthropic.com/settings/keys
    enabled: true
  tailscale:
    enabled: true
  ntopng:
    url: "http://localhost:3002"
    enabled: true
  suricata:
    log_path: "~/Repos/homelab/data/suricata/logs/eve.json"
    enabled: true
  uptime_kuma:
    url: "http://localhost:3001"
    username: "nimda"
    password: "nimda123"
    enabled: true
```

See [config/homelab-cli.yaml.example](config/homelab-cli.yaml.example) for full example.

## Development

```bash
make build                                            # Build
make test                                             # Tests
make install                                          # Build + install + codesign
```

**Stack:** .NET 8, Spectre.Console, Docker.DotNet, SSH.NET, YamlDotNet

**Workflow:** Feature branches + PRs. CI runs build, format check, tests. Never commit to main.

## Documentation

- [AI Monitoring](docs/AI_MONITORING.md) — how AI health reports work
- [Network Monitoring](docs/NETWORK_MONITORING.md) — nmap, ntopng, Suricata setup
- [TV Control](docs/TV_CONTROL.md) — LG WebOS pairing and commands
- [Self-Update](docs/SELF_UPDATE.md) — how binary updates work
- [Changelog](CHANGELOG.md) — version history

## License

MIT License - see [LICENSE](LICENSE).
