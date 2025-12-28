# 🏠 HomeLab CLI

> Command-line interface for managing Mac Mini M4 homelab services with Docker

[![Release](https://img.shields.io/github/v/release/moudlajs/homelab)](https://github.com/moudlajs/homelab/releases)
[![License](https://img.shields.io/github/license/moudlajs/homelab)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A beautiful, powerful CLI tool for managing your homelab with service-specific integrations for VPN, DNS, monitoring, and remote management. Built with ❤️ using Spectre.Console.

![Screenshot](docs/screenshot.png)

---

## ✨ Features

### 📊 Enhanced Status Dashboard
- **Service Discovery**: Auto-detect services from docker-compose.yml
- **Health Monitoring**: Combined Docker + service-specific health checks
- **Dependency Visualization**: Show service relationships and startup order
- **Watch Mode**: Live status updates with configurable refresh interval
- **Color Coding**: 🟢 Healthy, 🟡 Degraded, 🔴 Unhealthy
- **Service Metrics**: Display service-specific statistics

### 🔐 VPN Management (WireGuard)
- **Peer Management**: Add, remove, and list VPN peers
- **QR Code Generation**: Easy mobile device configuration
- **Key Generation**: Proper Curve25519 key generation with clamping
- **Configuration Export**: Generate peer configuration files

### 🌐 DNS Management (AdGuard Home)
- **Statistics Dashboard**: View DNS query statistics
- **Blocked Domains**: Top blocked domains with ranking
- **Visualizations**: Bar charts and formatted statistics
- **Filter Management**: Update DNS filters

### 📈 Monitoring Integration
- **Prometheus Alerts**: Display active alerts with severity coloring
- **Scrape Targets**: Monitor Prometheus scrape targets status
- **Grafana Dashboards**: List and open dashboards in browser
- **Metrics Querying**: Execute PromQL queries

### 🔒 Network Security & Monitoring
- **Device Discovery**: Scan network for connected devices with nmap
- **Port Scanning**: Identify open ports and running services
- **Traffic Analysis**: Monitor bandwidth usage and top talkers (ntopng)
- **Intrusion Detection**: Real-time security alerts with Suricata IDS
- **Network Status**: Comprehensive health dashboard
- **Export Support**: JSON, CSV, YAML formats for all commands

### 🌍 Remote Management
- **SSH Integration**: Manage remote homelabs via SSH
- **Connection Profiles**: Save and manage multiple remote connections
- **Remote Status**: Check remote homelab status and Docker info
- **File Sync**: Push/pull docker-compose files between local and remote
- **SSH Key Support**: Secure key-based authentication

### 🎮 Service Control
- Start, stop, and restart containers
- Interactive service selection
- Progress indicators for all operations

### ⚙️ Configuration Management
- View and edit `docker-compose.yml`
- Automatic timestamped backups
- Restore from previous configurations
- Integrates with your `$EDITOR`

### 🛠️ Maintenance Tools
- View container logs with customizable tail
- Update Docker images
- Clean up unused resources
- Track reclaimed disk space

---

## 📦 Installation

### macOS (ARM64)

```bash
# Download the latest release
curl -L https://github.com/moudlajs/homelab/releases/latest/download/homelab -o homelab

# Make it executable
chmod +x homelab

# Move to your PATH
sudo mv homelab /usr/local/bin/

# Verify installation
homelab --help
```

### Requirements

- macOS with Apple Silicon (M1/M2/M3/M4)
- Docker Desktop or OrbStack
- No .NET installation needed (self-contained binary)

---

## 🚀 Quick Start

```bash
# View homelab status
homelab status

# View status with dependency graph
homelab status --show-dependencies

# Watch mode (live updates every 5s)
homelab status --watch

# VPN: Add a new peer
homelab vpn add-peer danny-phone

# VPN: List all peers
homelab vpn status

# DNS: View statistics
homelab dns stats

# DNS: Show blocked domains
homelab dns blocked

# Monitoring: Show active alerts
homelab monitor alerts

# Monitoring: Check scrape targets
homelab monitor targets

# Monitoring: Open Grafana dashboard
homelab monitor dashboard

# Network: Scan for devices
homelab network scan

# Network: Display security alerts
homelab network intrusion --severity critical

# Network: Show overall network health
homelab network status

# Remote: Add a connection
homelab remote connect mac-mini 192.168.1.100 -u admin -k ~/.ssh/id_rsa

# Remote: Check remote status
homelab remote status

# Remote: Sync compose file
homelab remote sync --push

# Service control
homelab service start adguard
homelab service stop wireguard
homelab service restart grafana

# View logs
homelab logs adguard -n 500

# Clean up
homelab cleanup -v
```

---

## 📖 Commands

### `homelab status [options]`

Display a comprehensive dashboard of your homelab services with health monitoring and dependency tracking.

**Options:**
- `--show-dependencies` - Display service dependency graph
- `--watch` - Live status updates (refresh every 5s)
- `--interval <seconds>` - Custom refresh interval for watch mode

```bash
# Basic status
homelab status

# Show dependency graph
homelab status --show-dependencies

# Watch mode (live updates)
homelab status --watch

# Custom refresh interval
homelab status --watch --interval 10
```

**Features:**
- Service discovery from docker-compose.yml
- Combined Docker + service-specific health checks
- Color-coded status (🟢 Healthy, 🟡 Degraded, 🔴 Unhealthy)
- Service-specific metrics
- Dependency visualization

---

### `homelab vpn <command>`

Manage WireGuard VPN peers and configurations.

#### `homelab vpn status`

Display all VPN peers with connection statistics.

```bash
homelab vpn status
```

#### `homelab vpn add-peer <name>`

Add a new VPN peer with automatic configuration generation.

**Options:**
- `--ip <address>` - Assign specific IP address to peer
- `--qr` - Generate QR code for mobile devices (default: true)
- `--export` - Export configuration to file

```bash
# Add peer with QR code
homelab vpn add-peer danny-phone

# Add peer with specific IP
homelab vpn add-peer laptop --ip 10.8.0.5

# Export configuration file
homelab vpn add-peer server --export
```

**Features:**
- Automatic IP assignment
- Proper Curve25519 key generation
- QR code generation for mobile devices
- Configuration file export

#### `homelab vpn remove-peer <name>`

Remove a VPN peer.

```bash
homelab vpn remove-peer old-device
```

---

### `homelab dns <command>`

Manage DNS and ad-blocking with AdGuard Home.

#### `homelab dns stats`

Display DNS statistics with visualizations.

```bash
homelab dns stats
```

**Shows:**
- Total queries
- Blocked queries and percentage
- Safe browsing blocks
- Parental control blocks
- Query distribution bar chart

#### `homelab dns blocked [options]`

Show top blocked domains.

**Options:**
- `-n, --limit <count>` - Number of domains to display (default: 10)

```bash
# Top 10 blocked domains
homelab dns blocked

# Top 50 blocked domains
homelab dns blocked -n 50
```

---

### `homelab monitor <command>`

Monitor homelab metrics and alerts with Prometheus/Grafana.

#### `homelab monitor alerts`

Display active Prometheus alerts with severity coloring.

```bash
homelab monitor alerts
```

**Features:**
- Severity color coding (Critical: red, Warning: yellow)
- Active duration tracking
- Alert summaries and descriptions

#### `homelab monitor targets`

Show Prometheus scrape targets status.

```bash
homelab monitor targets
```

**Shows:**
- Target health (up/down)
- Last scrape time
- Scrape duration
- Target summary statistics

#### `homelab monitor dashboard [uid]`

List or open Grafana dashboards.

```bash
# List all dashboards
homelab monitor dashboard

# Open specific dashboard
homelab monitor dashboard prometheus-stats
```

**Features:**
- Dashboard listing with tags
- Star indicators for favorites
- Direct browser integration
- Dashboard URLs displayed

---

### `homelab network <command>`

Network security monitoring, device discovery, and intrusion detection.

#### `homelab network scan [options]`

Discover devices on your network using nmap.

**Options:**
- `--range <cidr>` - Network range to scan (default: from config)
- `--quick` - Quick scan without port detection (faster)
- `--output <format>` - Output format: table, json, csv, yaml
- `--export <file>` - Export results to file

```bash
# Scan default network range
homelab network scan

# Scan specific range
homelab network scan --range 192.168.1.0/24

# Quick scan (faster, no ports)
homelab network scan --quick

# Export to JSON
homelab network scan --output json --export devices.json
```

**Shows:**
- IP address, MAC address, hostname
- Vendor identification
- Device status (up/down)
- Open ports (unless --quick)

#### `homelab network ports --device <ip> [options]`

Scan open ports on a specific device.

**Options:**
- `--device <ip>` - Target device IP (required)
- `--common` - Only scan common ports (default: true)
- `--output <format>` - Output format: table, json, csv, yaml
- `--export <file>` - Export results to file

```bash
# Scan common ports
homelab network ports --device 192.168.1.10

# Scan all ports (slower)
homelab network ports --device 192.168.1.10 --all

# Export results
homelab network ports --device 192.168.1.10 --output csv --export ports.csv
```

**Shows:**
- Port number and protocol
- Service name and version
- Port state (open/closed/filtered)

#### `homelab network devices [options]`

List all network devices tracked by ntopng.

**Options:**
- `--active` - Show only active devices
- `--output <format>` - Output format: table, json, csv, yaml
- `--export <file>` - Export results to file

```bash
# List all tracked devices
homelab network devices

# Show only active devices
homelab network devices --active

# Export to YAML
homelab network devices --output yaml --export devices.yaml
```

**Shows:**
- Device name, IP, MAC address
- First seen / last seen timestamps
- Bytes sent / received
- Throughput (if active)
- Operating system

#### `homelab network traffic [options]`

Display network traffic statistics from ntopng.

**Options:**
- `--device <ip>` - Filter by specific device
- `--top <n>` - Number of top talkers (default: 10)
- `--output <format>` - Output format: table, json, csv, yaml
- `--export <file>` - Export results to file

```bash
# Show overall traffic stats
homelab network traffic

# Filter by device
homelab network traffic --device 192.168.1.10

# Show top 20 talkers
homelab network traffic --top 20
```

**Shows:**
- Total bytes transferred
- Active flows count
- Top talkers with ranking (🥇🥈🥉)
- Protocol distribution bar chart

#### `homelab network intrusion [options]`

Display security alerts from Suricata IDS.

**Aliases:** `homelab network alerts`

**Options:**
- `--severity <level>` - Filter by severity: critical, high, medium, low
- `--limit <n>` - Maximum alerts to show (default: 50)
- `--output <format>` - Output format: table, json, csv, yaml
- `--export <file>` - Export results to file

```bash
# Show all recent alerts
homelab network intrusion

# Show only critical alerts
homelab network intrusion --severity critical

# Limit to 100 alerts
homelab network intrusion --limit 100

# Export to JSON
homelab network intrusion --output json --export alerts.json
```

**Shows:**
- Time ago
- Severity (color-coded: 🔴 critical, 🟠 high, 🟡 medium, ⚪ low)
- Alert type and category
- Source and destination IPs/ports
- Protocol
- Alert summary counts

#### `homelab network status`

Comprehensive network health dashboard combining all monitoring services.

**Alias:** `homelab network st`

```bash
homelab network status
```

**Shows:**
- Service health (nmap, ntopng, Suricata)
- Active devices count
- Total network traffic
- Top 5 bandwidth consumers
- Security alerts (last 24 hours)
- Latest critical alert
- Overall status (healthy/warning/critical)

**Features:**
- All-in-one network overview
- Color-coded service status
- Real-time threat detection
- Traffic analysis summary

📖 **Full Documentation:** See [Network Monitoring Guide](docs/NETWORK_MONITORING.md) for detailed setup, troubleshooting, and advanced usage.

---

### `homelab remote <command>`

Manage remote homelab connections via SSH.

#### `homelab remote connect <name> <host> [options]`

Add or update a remote connection profile.

**Options:**
- `-u, --username <user>` - SSH username
- `-p, --port <port>` - SSH port (default: 22)
- `-k, --key-file <path>` - Path to SSH private key
- `--docker-socket <path>` - Docker socket path on remote
- `--compose-file <path>` - docker-compose.yml path on remote
- `--default` - Set as default connection
- `--test` - Test connection before saving (default: true)

```bash
# Add connection with SSH key
homelab remote connect mac-mini 192.168.1.100 -u admin -k ~/.ssh/id_rsa --default

# Add with custom ports and paths
homelab remote connect server 10.0.0.5 -u root --port 2222 --compose-file /opt/homelab/docker-compose.yml
```

**Features:**
- Connection testing before saving
- Docker status verification
- SSH key authentication
- Profile persistence in ~/.homelab/remotes.yaml

#### `homelab remote list`

List all configured remote connections.

```bash
homelab remote list
```

**Shows:**
- Connection name
- Host and port
- Username
- Default connection indicator (⭐)
- Last connected time

#### `homelab remote status [name]`

Check status of remote homelab.

```bash
# Use default connection
homelab remote status

# Specific connection
homelab remote status mac-mini
```

**Shows:**
- SSH connection status
- Docker version and status
- System info (CPUs, memory)
- Running containers

#### `homelab remote sync [name] [options]`

Sync docker-compose files between local and remote.

**Options:**
- `--push` - Push local file to remote
- `--pull` - Pull remote file to local
- `--local-file <path>` - Local file path (default: docker-compose.yml)
- `--remote-file <path>` - Remote file path

```bash
# Push local to remote (default connection)
homelab remote sync --push

# Pull from specific remote
homelab remote sync mac-mini --pull

# Custom file paths
homelab remote sync --push --local-file custom-compose.yml
```

**Features:**
- Bidirectional sync
- Overwrite confirmation
- File size reporting
- SFTP file transfer

#### `homelab remote remove <name> [options]`

Remove a remote connection profile.

**Options:**
- `-y, --yes` - Skip confirmation prompt

```bash
# Remove with confirmation
homelab remote remove old-server

# Skip confirmation
homelab remote remove old-server -y
```

---

### `homelab service <action> <name>`

Control service lifecycle.

**Actions:** `start`, `stop`, `restart`

```bash
# Start a service
homelab service start adguard

# Stop a service
homelab service stop grafana

# Restart a service
homelab service restart wireguard
```

---

### `homelab config [action]`

Manage your docker-compose configuration.

**Actions:** `view`, `edit`, `backup`, `restore`, `list-backups`

```bash
# View current configuration
homelab config view

# Edit configuration (opens in $EDITOR)
homelab config edit

# Create a backup
homelab config backup

# List available backups
homelab config list-backups

# Restore from a backup
homelab config restore

# Restore specific backup
homelab config restore --backup docker-compose.20231227_103045.yml.bak
```

**Features:**
- Automatic backup before any edits
- Timestamped backup files
- Interactive backup selection
- Confirmation prompts for destructive operations

---

### `homelab logs <container> [options]`

View container logs.

**Options:**
- `-n, --lines <COUNT>` - Number of lines to display (default: 100)

```bash
# View last 100 lines (default)
homelab logs adguard

# View last 500 lines
homelab logs adguard -n 500

# View all available logs
homelab logs grafana -n 10000
```

---

### `homelab update <image>`

Pull the latest version of a Docker image.

```bash
# Update nginx
homelab update nginx

# Update specific tag
homelab update postgres:14
```

---

### `homelab cleanup [options]`

Clean up Docker resources to reclaim disk space.

**Options:**
- `-v, --volumes` - Also remove unused volumes
- `-f, --force` - Skip confirmation prompt

```bash
# Clean up containers and images (with confirmation)
homelab cleanup

# Also clean up volumes
homelab cleanup -v

# Skip confirmation
homelab cleanup -f

# Clean everything without prompts
homelab cleanup -vf
```

**What it cleans:**
- Stopped containers
- Dangling images (not used by any container)
- Unused volumes (with `-v` flag)

---

## 🏗️ Architecture

Built with clean architecture principles:

```
┌─────────────────────────────────────────┐
│         CLI Layer (Commands)            │
│   StatusCommand, ServiceCommand, etc.   │
├─────────────────────────────────────────┤
│       Business Logic (Services)         │
│  DockerService, BackupService, etc.     │
├─────────────────────────────────────────┤
│           Models (DTOs)                 │
│  ServiceStatus, HealthCheck, etc.       │
├─────────────────────────────────────────┤
│      Infrastructure (External)          │
│  Docker SDK, File system, HTTP calls    │
└─────────────────────────────────────────┘
```

**Technologies:**
- [.NET 8](https://dotnet.microsoft.com/) - Cross-platform framework
- [Spectre.Console](https://spectreconsole.net/) - Beautiful terminal UI
- [Docker.DotNet](https://github.com/dotnet/Docker.DotNet) - Docker API SDK
- [SSH.NET](https://github.com/sshnet/SSH.NET) - SSH/SFTP client
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) - YAML parsing
- [QRCoder](https://github.com/codebude/QRCoder) - QR code generation
- System.Net.Http.Json - HTTP API integration
- Dependency Injection with Microsoft.Extensions

---

## 🔧 Development

### Prerequisites

- .NET 8 SDK
- Docker Desktop or OrbStack
- JetBrains Rider (recommended) or VS Code

### Building from Source

```bash
# Clone the repository
git clone https://github.com/moudlajs/homelab.git
cd homelab

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/HomeLab.Cli -- status

# Build release
dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained \
  -o ./bin/release \
  /p:PublishSingleFile=true
```

### Project Structure

```
homelab/
├── src/
│   └── HomeLab.Cli/
│       ├── Commands/           # CLI commands
│       │   ├── Dns/            # DNS management
│       │   ├── Monitor/        # Monitoring commands
│       │   ├── Remote/         # Remote management
│       │   └── Vpn/            # VPN management
│       ├── Services/           # Business logic
│       │   ├── AdGuard/        # AdGuard Home integration
│       │   ├── Configuration/  # Config management
│       │   ├── Dependencies/   # Dependency graph
│       │   ├── Docker/         # Docker operations
│       │   ├── Grafana/        # Grafana integration
│       │   ├── Health/         # Health checks
│       │   ├── Prometheus/     # Prometheus integration
│       │   ├── Remote/         # SSH/Remote services
│       │   ├── ServiceDiscovery/ # Service discovery
│       │   └── WireGuard/      # WireGuard VPN
│       ├── Models/             # Data models
│       └── Program.cs          # Entry point
├── config/                     # Configuration files
├── docs/                       # Documentation
└── CHANGELOG.md                # Version history
```

---

## 📝 Configuration

HomeLab CLI expects your `docker-compose.yml` at:
```
~/homelab/docker-compose.yml
```

Backups are stored at:
```
~/homelab/backups/
```

You can customize these paths by modifying `ConfigService.cs`.

---

## 🐛 Troubleshooting

### "Container not found"

Make sure your containers are prefixed with `homelab_` or run:
```bash
docker ps -a
```
to see all container names.

### "Docker socket not found"

Ensure Docker Desktop or OrbStack is running. The CLI connects to:
```
unix:///var/run/docker.sock
```

### Binary not executable

Run:
```bash
chmod +x /usr/local/bin/homelab
```

---

## 🗺️ Roadmap

### ✅ Completed (v1.5.0)

- [x] Service dependency management
- [x] Remote homelab management (SSH)
- [x] Health check monitoring
- [x] Service-specific integrations (VPN, DNS, Monitoring)

### 🚧 Future Enhancements

- [ ] Real-time log following (`-f` flag)
- [ ] Automated backup scheduling
- [ ] Notification integrations (Slack, Discord, Webhook)
- [ ] Container resource monitoring (CPU, memory, network)
- [ ] TUI (Terminal UI) mode with live updates
- [ ] Multi-homelab orchestration
- [ ] Automated service updates with rollback
- [ ] Integration testing framework
- [ ] Performance metrics collection

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Spectre.Console](https://spectreconsole.net/) - For the amazing terminal UI framework
- [Docker.DotNet](https://github.com/dotnet/Docker.DotNet) - For the Docker API SDK
- Built with [Claude Code](https://claude.com/claude-code)

---

## 📧 Contact

**Author:** Daniel Czetner
**GitHub:** [@moudlajs](https://github.com/moudlajs)
**Project Link:** [https://github.com/moudlajs/homelab](https://github.com/moudlajs/homelab)

---

<div align="center">

**⭐ Star this repo if you find it useful! ⭐**

Made with ❤️ for the homelab community

</div>
