# Changelog

All notable changes to the HomeLab CLI project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.14.0] - 2026-02-20

### Added - Extended TV Controls
- **`homelab tv screen off|on`** — turn screen off/on without full power cycle (TV stays running)
- **`homelab tv input [ID]`** — list all HDMI/external input sources or switch to one
- **`homelab tv sound [OUTPUT]`** — show current sound output or change device
- **`homelab tv channel [NUM|up|down]`** — show current channel, list all, or tune
- **`homelab tv info`** — display system info, software version, power state
- **`homelab tv notify "message"`** — send toast notification to TV screen
- **`homelab tv settings --get/--set`** — read/write TV system settings (picture, energy saving, etc.)

### Fixed
- **ntopng API integration** — disabled login requirement, auto-discover network interface, fixed response DTO mapping
- **Dashboard redesign** — full homelab overview with system gauges, network/VPN, containers, anomalies

### Changed
- Refactored TV commands to use shared `TvCommandHelper` (eliminated 8x code duplication)
- Added Makefile with `make install` for ARM64 codesign workflow

## [1.13.0] - 2026-02-14

### Added - Enhanced Network Monitoring

#### Network Anomaly Detection
- **`homelab network analyze [--last 24h] [--ai]`** — analyze network history for anomalies
- Detects new devices, device disappearances, traffic spikes (>3x rolling avg), security alert escalation, device count anomalies (>30% change)
- Optional `--ai` flag sends analysis to Claude for AI-powered insights

#### Enriched Event Snapshots
- Periodic collector now gathers **ntopng traffic data** (bytes, flows, top talkers) and **Suricata IDS alerts** (severity counts, signatures) in parallel alongside nmap
- Graceful degradation when ntopng/Suricata are offline

#### Enhanced Monitor Output
- `monitor history` — new Traffic and Alerts columns with severity-colored counts
- `monitor collect` — network summary line (devices, traffic, alerts)
- `monitor ask` / `monitor report` — AI prompts enriched with network device changes, security events, traffic trends

### Changed - Infrastructure

#### Event Log Storage
- Event logs moved to external Samsung T9 drive (`/Volumes/T9/.homelab/events.jsonl`) with automatic fallback to `~/.homelab/`
- Collection interval changed from 5min to 10min

#### CI/CD
- CodeQL analysis moved from `macos-latest` to `ubuntu-latest` for reliability

### Added - Tests
- 10 tests for NetworkAnomalyDetector (all detection rules + edge cases)
- 14 tests for EventCollector
- 9 tests for HomelabConfigService
- 11 tests for ServiceHealthCheckService

---

## [1.12.0] - 2026-02-09

### Changed - Codebase Cleanup & Redesign

#### Removed Dead Code (-3,074 lines)
- **WireGuard removed** — all 8 files deleted (replaced by Tailscale)
- **Quick commands removed** — all 5 files deleted (fake implementations)
- **Mock services removed** — all 8 files deleted (never used, no tests)
- **`UseMockServices` config removed** — dead toggle

#### VPN Namespace Unification
- `homelab tailscale status/up/down/devices` renamed to `homelab vpn status/up/down/devices`
- User-facing abstraction — command surface doesn't expose service name

#### TUI Dashboard Fix
- Removed fake uptime/speed panels (showed mock data silently when services offline)
- Replaced with real Docker container list panel

#### Shell & Completions Update
- Shell help now shows all commands grouped by category (was showing 7 out of 50+)
- Updated bash, zsh, and tab completions to match current command surface
- Removed dead quick-*, tailscale, WireGuard vpn references

#### Other Fixes
- TraefikClient returns empty data on failure instead of mock fallbacks
- Health check uses Tailscale client for VPN type
- Removed broken Prometheus Docker metrics target
- Removed wireguard service from docker-compose

### Added - AI-Powered Monitoring

#### AI Health Reports
- **`homelab monitor report`** — AI-generated health summary using Claude Haiku
- **`homelab monitor report --raw`** — raw data only, no API call
- **`homelab monitor ask "question"`** — ask anything about your homelab state
- Collects data from 4 sources in parallel: system metrics, Docker, Prometheus, network
- Cost: ~$0.001-0.002 per query (~$0.50/month at 5x/day)

#### Network Data Collection
- nmap device discovery integrated into AI data collector
- ntopng traffic stats fed to AI analysis
- Suricata IDS alerts included in health reports

#### Monitoring Containers
- ntopng container configured and running (port 3002)
- Suricata IDS container with ET Open rules (47,942 rules)
- Both containers in docker-compose.yml with proper config

---

## [1.11.0] - 2026-02-08

### Added - Interactive Shell, Tailscale & Self-Update Overhaul

#### Interactive Shell
- **Shell mode**: `homelab shell` or just `homelab` — REPL with tab completion and command history
- Tab completion for all commands, subcommands, and aliases
- ReadLine-based input with history persistence

#### Tailscale VPN Integration
- **`homelab vpn status`** — Display VPN connection status (Tailscale)
- **`homelab vpn up`** — Connect to VPN
- **`homelab vpn down`** — Disconnect from VPN
- **`homelab vpn devices`** — List all devices on the tailnet

#### Graceful Docker Handling
- `homelab status` and `homelab tui` detect when Docker is unavailable
- Shows friendly message instead of connection error spam
- Non-Docker commands (tv, vpn, network) work regardless

#### TV Fix
- `homelab tv launch default` now works correctly

### Fixed - Self-Update Reliability

#### Crash-Safe Self-Update
- **Backup before overwrite**: Current binary backed up to `.bak` before installing
- **Download validation**: Rejects corrupt downloads (files < 1KB)
- **Post-install verification**: Runs `homelab version` to confirm new binary works
- **Automatic rollback**: Restores backup if code signing or verification fails
- **macOS signing**: Clears quarantine (`xattr -cr`) and ad-hoc signs (`codesign -f -s -`) — fixes `zsh: killed`

#### Smart Install Path
- Auto-detects binary location from running process
- Falls back to `~/.local/bin/homelab`
- No unnecessary `sudo` for user-owned paths

#### Version Parsing Fix
- Strips git hash suffix from `AssemblyInformationalVersion` (e.g., `1.0.0+abc123` → `1.0.0`)
- `Version.TryParse` now works correctly for version comparison

#### Download Progress
- Progress bar with percentage, transfer speed, and ETA (replaces spinner)

#### Documentation
- Added `docs/SELF_UPDATE.md` — usage, crash safety, manual recovery

---

## [1.8.0] - 2025-12-28

### Added - Shell Completion Support

#### Developer Experience Enhancement ⚡
- **Completion Command**: `homelab completion <shell>` - Generate shell completion scripts
  - Bash completion: `homelab completion bash`
  - Zsh completion: `homelab completion zsh`
  - Tab completion for all 28 commands and 42 subcommands
  - Completion for command aliases (st, svc, qr, qu, qb, qf, etc.)
  - Completion for common flags (--output, --export)

#### Installation
```bash
# Bash
homelab completion bash > /usr/local/etc/bash_completion.d/homelab

# Zsh
homelab completion zsh > ~/.zsh/completions/_homelab
```

#### Benefits
- ⚡ Faster command entry with tab completion
- 🧠 Discover available commands and subcommands
- 🎯 Reduce typos with auto-completion
- 📚 Built-in documentation via completion descriptions (zsh)

---

## [1.7.0] - 2025-12-28

### Added - Phase 11: Traefik Integration (Extended)

#### Traefik Backend Services & Middlewares 🔄
- **Services Command**: `homelab traefik services` - Display all backend services with load balancer configuration
  - Shows service type, servers, load balancer algorithm, and health status
  - Full export support (JSON, CSV, YAML)
- **Middlewares Command**: `homelab traefik middlewares` (alias: `mw`) - List all HTTP middlewares
  - Displays middleware type, status, and configuration details
  - Shows auth, compression, headers, redirect configurations
  - Full export support (JSON, CSV, YAML)

#### Export Functionality
- Both commands support `--output <format>` (json, csv, yaml)
- Both commands support `--export <file>` for file output
- Consistent export pattern across all Traefik commands

---

## [1.6.1] - 2025-12-28

### Fixed

#### Self-Update Bug Fixes
- **DI Resolution Error** - Fixed "Could not resolve type 'SelfUpdateCommand'" error caused by YAML property name mismatch during config loading
- **Config File Loading** - Updated config service to check `~/.config/homelab/homelab-cli.yaml` first (standard user config location), falling back to repo config for development
- **YAML Property Naming** - Corrected `GitHub` → `Github` in `HomelabConfig` to match YamlDotNet's `UnderscoredNamingConvention`

### Changed
- Config path resolution now prioritizes user config directory (`~/.config/homelab/`)
- TypeRegistrar code style improvements (expression-bodied → block-bodied members)

---

## [1.6.0] - 2025-12-28

### Added - Phase 10: Self-Update Command

#### Self-Update Functionality ✨
- **Version Command**: `homelab version` - Display version information (product, version, runtime, platform)
- **Self-Update Command**: `homelab self-update` - Update to latest release from GitHub
  - `--check` - Check for updates without installing
  - `--version <VERSION>` - Install specific version (e.g., v1.6.0 or 1.6.0)
  - `--force` - Skip confirmation prompt
- **GitHub API Integration**:
  - GitHubReleaseService for fetching releases
  - Semantic version comparison (1.6.0 > 1.5.0)
  - Binary download from release assets
  - Automatic self-installation (requires sudo)
  - GitHub token authentication for private repos

#### Phase 9a Complete
- **Uptime Remove Command**: `homelab uptime rm <id>` - Remove monitors from Uptime Kuma

### Changed
- **Breaking**: `homelab update` renamed to `homelab image-update` (for Docker images)
  - Old: `homelab update nginx`
  - New: `homelab image-update nginx`

### Technical Improvements
- New service: `IGitHubReleaseService` and `GitHubReleaseService`
- New commands: `VersionCommand` and `SelfUpdateCommand`
- Version information added to project file (1.6.0)
- Cross-platform binary support (macOS ARM64)

---

## [1.5.0] - 2025-12-28

### Added - Phase 8: Home Assistant Smart Home Integration

#### Home Assistant CLI Control
Complete integration with Home Assistant for smart home device management:

- **Status Command**: `homelab ha status` (aliases: `st`, `ls`)
  - Display all entities grouped by domain (lights, switches, sensors, climate, etc.)
  - Color-coded state indicators
  - Entity attributes display
  - Friendly name and ID mapping

- **Control Command**: `homelab ha control <action> <entity>`
  - Actions: `on`, `off`, `toggle`
  - Supports all controllable domains (light, switch, climate, etc.)
  - Immediate state updates with confirmation

- **Get Command**: `homelab ha get <entity>`
  - Get detailed entity information with all attributes
  - State history and last updated timestamp
  - Device-specific attributes (brightness, temperature, etc.)

- **List Command**: `homelab ha list <domain>`
  - List all entities by domain (light, switch, sensor, binary_sensor, climate, etc.)
  - Domain filtering
  - Count summaries

#### Full Export Support
All HA commands support multiple output formats:
- `--output table` (default) - Beautiful terminal tables
- `--output json` - JSON format for automation
- `--output csv` - CSV for spreadsheets
- `--output yaml` - YAML for configuration
- `--export <file>` - Save to file

#### REST API Integration
- **HomeAssistantClient** - REST API integration with bearer token authentication
- **Configuration** via `homelab-cli.yaml`:
  ```yaml
  services:
    homeassistant:
      url: http://localhost:8123
      token: your-long-lived-access-token
      enabled: true
  ```
- Automatic mock data fallback for testing
- Ready for Mac Mini deployment

#### Mock Data for Testing
Includes 7 mock entities for testing without HA installed:
- 2 lights (living room, bedroom)
- 1 switch (christmas lights)
- 2 sensors (temperature, humidity)
- 1 binary sensor (front door)
- 1 climate (thermostat)

---

## [1.4.0] - 2025-12-27

### Added - Phase 7: Quick Actions & Universal Export Support

#### Quick Actions (Ultra-Fast Commands)
No confirmation prompts - perfect for daily operations:
- **Quick Restart**: `homelab qr <service>` (alias for `quick-restart`)
  - Instant restart without confirmation
  - Shows restart status

- **Quick Update**: `homelab qu <service>` (alias for `quick-update`)
  - Pull latest image and restart
  - No prompts, just does it

- **Quick Backup**: `homelab qb <service>` (alias for `quick-backup`)
  - Backup container configs instantly
  - Timestamped backups

- **Quick Fix**: `homelab qf <service>` (alias for `quick-fix`)
  - Stop, clear cache, restart
  - Troubleshooting in one command

#### Universal Export Support
All commands now support `--output` and `--export` flags:
- **VPN**: `homelab vpn status --output json --export peers.json`
- **DNS**: `homelab dns stats --export stats.csv`
- **Monitor**: `homelab monitor alerts --output yaml`
- **Uptime**: `homelab uptime status --export monitors.json`
- **Speedtest**: `homelab speedtest stats --output csv`

Supported formats:
- `table` - Terminal table (default)
- `json` - JSON format
- `csv` - CSV spreadsheet
- `yaml` - YAML format

#### Enhanced TUI Dashboard
- Added Uptime Kuma monitoring panel with service health
- Added Speedtest Tracker speed panel with latest results
- Real-time service health visualization
- System info with Docker stats (containers, images, memory)
- Auto-refresh with configurable interval

### Technical Improvements
- Extended export functionality across all service commands
- Standardized output formatting infrastructure
- Enhanced BaseExportCommand for reusability

---

## [1.3.0] - 2025-12-27

### Added - Phase 6: Monitoring & Observability

#### Export Commands for All Services
Export any command output in multiple formats for automation and analysis:

**Formats:**
- **JSON** - For automation, scripts, and APIs
- **CSV** - For spreadsheets and data analysis
- **YAML** - For configuration files
- **Table** - Human-readable (default)

**Usage:**
```bash
# Export to stdout
homelab status --output json
homelab status --output csv
homelab status --output yaml

# Export to file
homelab status --output json --export status.json
homelab status --output csv --export services.csv
```

**Use Cases:**
- Pipe to jq for filtering: `homelab status -o json | jq '.[] | select(.IsHealthy == false)'`
- Import to Excel/Google Sheets
- Send to monitoring systems
- Automate with scripts

#### Uptime Kuma Integration
Track service uptime and monitor availability:

**Commands:**
- `homelab uptime status` (aliases: `st`, `ls`) - Show all monitored services
- `homelab uptime alerts` (alias: `al`) - Show recent incidents
- `homelab uptime add <name> <url>` - Add new monitor

**Features:**
- Real-time uptime percentage (99.98%, 100%, etc.)
- Visual status indicators (🟢 Up, 🔴 Down)
- Average response time tracking
- Incident history with duration
- Summary statistics (up/down counts, avg uptime)

#### Speedtest Tracker Integration
Monitor internet connection speed over time:

**Commands:**
- `homelab speedtest run` - Run a new speed test
- `homelab speedtest stats` (alias: `st`) - View statistics and history

**Features:**
- Download/upload speed tracking (Mbps)
- Ping latency monitoring
- 30-day statistics (average, min, max)
- Historical trends with visual bar charts
- Color-coded speed indicators
- Server and ISP information

### Technical Improvements

#### New Services
- **OutputFormatter** (`IOutputFormatter`) - Multi-format export (JSON/CSV/YAML)
- **UptimeKumaClient** - Uptime Kuma API integration
- **SpeedtestClient** - Speedtest Tracker API integration

#### New Infrastructure
- **BaseExportCommand** - Reusable export functionality for commands
- Extended **ServiceClientFactory** with Uptime Kuma and Speedtest clients
- **CsvHelper 33.1.0** - Professional CSV export

#### Testing Support
- Mock data support for offline testing
- Health check integration
- Error handling with helpful messages
- Graceful degradation when services unavailable

---

## [1.2.0] - 2025-12-27

### Added - Quality of Life Improvements

#### TUI Mode - Live Dashboard
- **Live Dashboard Command**: `homelab tui` (aliases: `ui`, `dashboard`)
  - Real-time service health monitoring
  - Auto-refreshing dashboard (configurable interval)
  - Docker system information display
  - Service status table with health indicators
  - Container and image statistics
  - Graceful Ctrl+C shutdown
  - Beautiful Spectre.Console layout with panels

#### Command Aliases (18 shortcuts!)
- **Main Commands**:
  - `homelab st` → `homelab status`
  - `homelab svc` → `homelab service`
  - `homelab ui` → `homelab tui`
  - `homelab dashboard` → `homelab tui`
- **VPN Commands**:
  - `homelab vpn ls` → `homelab vpn status`
  - `homelab vpn list` → `homelab vpn status`
  - `homelab vpn add` → `homelab vpn add-peer`
  - `homelab vpn rm` → `homelab vpn remove-peer`
  - `homelab vpn remove` → `homelab vpn remove-peer`
- **DNS Commands**:
  - `homelab dns st` → `homelab dns stats`
  - `homelab dns bl` → `homelab dns blocked`
- **Monitor Commands**:
  - `homelab monitor al` → `homelab monitor alerts`
  - `homelab monitor tg` → `homelab monitor targets`
  - `homelab monitor dash` → `homelab monitor dashboard`
  - `homelab monitor db` → `homelab monitor dashboard`

#### Enhanced Docker Service
- **System Information**: `GetSystemInfoAsync()` method
  - Docker version and operating system
  - Architecture and CPU count
  - Total memory information
  - Container counts (running/stopped/total)
  - Image count
  - Used in TUI dashboard system panel

### Testing
- Comprehensive testing with real Docker services
- Tested with AdGuard Home, WireGuard, Prometheus, Grafana, Node Exporter
- All 18 aliases verified working
- TUI mode live update tested
- Complete test report in `TEST_ALL_FEATURES.md`

### Documentation
- **New Files**:
  - `TEST_ALL_FEATURES.md` - Comprehensive test results
  - `docs/HOW_TO_USE.md` - Beginner-friendly usage guide
  - `docs/QUICK_START.md` - Technical reference
  - `docs/TESTING_REPORT.md` - Initial test results
  - `docs/FINAL_SUMMARY.md` - Complete feature summary
  - `docker-compose.yml` - Local testing setup

---

## [1.1.0] - 2025-12-27

### Added - Phase 5: HomeLab-Specific Service Integration

#### Service Discovery & Health Checks (Day 2)
- **Service Discovery**: Auto-detect homelab services from docker-compose.yml
  - `ComposeFileParser` for parsing compose files
  - Service type classification (DNS, VPN, Monitoring, Dashboard, Metrics)
  - Automatic service detection from docker-compose
- **Enhanced Health Checks**: Service-specific health monitoring
  - `ServiceHealthCheckService` for orchestrating health checks
  - Combined Docker + service-specific health status
  - Three-level health status (Healthy/Degraded/Unhealthy)
  - Service-specific metrics display
- **Enhanced Status Command**:
  - Color-coded service status (🟢 Healthy, 🟡 Degraded, 🔴 Unhealthy)
  - Service type identification
  - Service-specific metrics in status output
  - Beautiful tables with rounded borders

#### VPN Management (Day 3)
- **WireGuard Integration**: Full WireGuard peer management
  - `WireGuardClient` for managing WireGuard configurations
  - Proper Curve25519 key generation with clamping
  - Peer configuration file management
- **VPN Commands**:
  - `homelab vpn status` - Display all VPN peers and statistics
  - `homelab vpn add-peer <name>` - Generate new peer configurations with QR codes
  - `homelab vpn remove-peer <name>` - Remove VPN peers
- **QR Code Generation**: Mobile device configuration
  - QRCoder integration for easy mobile setup
  - Console-friendly QR code display
  - Peer configuration export

#### DNS Management (Day 4)
- **AdGuard Home Integration**: DNS statistics and management
  - `AdGuardClient` for AdGuard Home API integration
  - DNS query statistics tracking
  - Blocked domain monitoring
  - Filter management
- **DNS Commands**:
  - `homelab dns stats` - Display DNS statistics with visualizations
  - `homelab dns blocked` - Show top blocked domains with ranking
- **Beautiful Visualizations**:
  - Bar charts for query distribution
  - Formatted statistics panels
  - Color-coded metrics

#### Monitoring Integration (Day 4)
- **Prometheus Integration**: Metrics and alerting
  - `PrometheusClient` for Prometheus API integration
  - Active alerts retrieval
  - Scrape targets monitoring
  - PromQL query execution
- **Grafana Integration**: Dashboard management
  - `GrafanaClient` for Grafana API integration
  - Dashboard listing and filtering
  - Browser integration for opening dashboards
- **Monitor Commands**:
  - `homelab monitor alerts` - Display active Prometheus alerts with severity coloring
  - `homelab monitor targets` - Show Prometheus scrape targets status
  - `homelab monitor dashboard [uid]` - List or open Grafana dashboards

#### Service Dependencies (Day 5)
- **Dependency Graph**: Service relationship management
  - `ServiceDependencyGraph` for dependency tracking
  - Hard vs Soft dependency types
  - Topological sorting for startup order
  - Circular dependency detection
- **Enhanced Status Features**:
  - `--show-dependencies` flag - Display service dependency graph
  - `--watch` flag - Live status updates with configurable refresh
  - `--interval <seconds>` flag - Custom refresh interval
  - Beautiful tree visualization of dependencies
  - Dependency health checking
- **Predefined Dependencies**:
  - Grafana → Prometheus (Hard dependency)
  - Prometheus → Node Exporter (Soft dependency)

#### Remote Management (Day 6)
- **SSH Integration**: Remote homelab management
  - `SshService` using SSH.NET library
  - SSH key-based authentication
  - Command execution on remote hosts
  - File transfer via SFTP
- **Connection Profiles**: Persistent remote connections
  - `RemoteConnectionService` for profile management
  - Stored in `~/.homelab/remotes.yaml`
  - Default connection support
  - Last connected timestamp tracking
- **Remote Commands**:
  - `homelab remote connect <name> <host>` - Add/update remote connections
  - `homelab remote list` - List all configured connections
  - `homelab remote status [name]` - Check remote homelab status
  - `homelab remote sync --push|--pull` - Sync docker-compose files
  - `homelab remote remove <name>` - Remove connection profiles

### Changed
- **Status Command**: Now supports settings for watch mode and dependency visualization
- **Service Factory**: All real service clients implemented (AdGuard, Prometheus, Grafana, WireGuard)
- **Configuration System**: Enhanced with service-specific configurations

### Technical Improvements
- **Service Abstraction Layer**: Interface-based design for all services
  - `IServiceClient` base interface
  - `IAdGuardClient`, `IWireGuardClient`, `IPrometheusClient`, `IGrafanaClient`
  - `ISshService` for remote operations
  - Factory pattern for real/mock service switching
- **HTTP Client Integration**: System.Net.Http.Json for API calls
  - Clean JSON serialization
  - Basic authentication support
  - Proper error handling
- **SSH.NET Library**: Robust remote management
  - Connection testing
  - Command execution
  - File transfer (upload/download)
  - Multiple authentication methods
- **YamlDotNet**: Configuration and profile storage
  - docker-compose.yml parsing
  - Remote connection profiles
  - Service configuration
- **Spectre.Console**: Enhanced UI
  - Tree visualizations
  - Bar charts
  - Figlet headers
  - Status spinners
  - Color-coded output

### Dependencies Added
- SSH.NET (2024.2.0) - SSH/SFTP client
- QRCoder - QR code generation
- System.Net.Http.Json - HTTP API calls

---

## [1.0.0] - 2025-12-27

### Initial Release - Phase 1-4

#### Phase 1: Foundation & Status Dashboard
- Basic Docker integration
- Container status monitoring
- Service health checks
- Status dashboard with Spectre.Console

#### Phase 2: Service Lifecycle Control
- `homelab service start <service>` - Start containers
- `homelab service stop <service>` - Stop containers
- `homelab service restart <service>` - Restart containers

#### Phase 3: Configuration Management
- `homelab config view` - View current configuration
- `homelab config edit` - Edit configuration files
- `homelab config backup` - Backup configurations
- `homelab config restore` - Restore from backup

#### Phase 4: Maintenance & Automation
- `homelab logs <container>` - View container logs
- `homelab update <image>` - Update container images
- `homelab cleanup` - Clean up unused Docker resources

### Technical Foundation
- .NET 8 target framework
- Spectre.Console for beautiful CLI output
- Docker.DotNet for Docker API integration
- YamlDotNet for YAML configuration
- Dependency injection with Microsoft.Extensions.DependencyInjection
- Async/await throughout for performance

---

[1.12.0]: https://github.com/moudlajs/homelab/compare/v1.11.0...v1.12.0
[1.11.0]: https://github.com/moudlajs/homelab/compare/v1.8.0...v1.11.0
[1.8.0]: https://github.com/moudlajs/homelab/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/moudlajs/homelab/compare/v1.6.1...v1.7.0
[1.6.1]: https://github.com/moudlajs/homelab/compare/v1.6.0...v1.6.1
[1.6.0]: https://github.com/moudlajs/homelab/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/moudlajs/homelab/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/moudlajs/homelab/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/moudlajs/homelab/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/moudlajs/homelab/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/moudlajs/homelab/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/moudlajs/homelab/releases/tag/v1.0.0
