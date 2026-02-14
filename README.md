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
homelab monitor collect                 Capture event snapshot
homelab monitor history [--last 24h]    Event timeline with change detection
homelab monitor schedule install|uninstall  Periodic collection (10min)
homelab monitor report [--raw]          AI health summary (Claude Haiku)
homelab monitor ask "<question>"        Ask AI about your homelab
homelab monitor alerts                  Prometheus alerts
homelab monitor targets                 Prometheus scrape targets
homelab monitor dashboard               Grafana dashboards
```

Collects system metrics, Docker state, Prometheus data, and network info — sends to Claude for analysis. ~$0.001/query. Event snapshots stored on external drive with 7-day retention.

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
homelab tv apps                         List installed apps
homelab tv launch <app>                 Launch app
homelab tv key <key>                    Send remote key
homelab tv status                       Connection status
```

See [docs/TV_CONTROL.md](docs/TV_CONTROL.md).

### Other

```
homelab ha status|control|get|list      Home Assistant
homelab traefik status|routes|...       Traefik reverse proxy
homelab uptime status|alerts|add|remove Uptime Kuma
homelab speedtest run|stats             Speed testing
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
  prometheus:
    url: "http://localhost:9090"
    enabled: true
  ai:
    provider: "anthropic"
    model: "claude-haiku-4-5-20251001"
    token: ""  # https://console.anthropic.com/settings/keys
    enabled: true
  tailscale:
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

---

Built with [Claude Code](https://claude.com/claude-code)
