# 🏠 HomeLab CLI

> Command-line interface for managing Mac Mini M4 homelab services with Docker

[![Release](https://img.shields.io/github/v/release/moudlajs/homelab)](https://github.com/moudlajs/homelab/releases)
[![License](https://img.shields.io/github/license/moudlajs/homelab)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A beautiful, powerful CLI tool for managing your homelab Docker containers, built with ❤️ using Spectre.Console.

![Screenshot](docs/screenshot.png)

---

## ✨ Features

### 📊 Status Dashboard
- View all container statuses at a glance
- Health monitoring
- Beautiful table output with color coding

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

# Start a service
homelab service start adguard

# Stop a service
homelab service stop wireguard

# Restart a service
homelab service restart grafana

# View logs (last 100 lines)
homelab logs adguard

# View more logs
homelab logs adguard -n 500

# Update an image
homelab update nginx:latest

# Clean up unused containers and images
homelab cleanup

# Also clean up volumes
homelab cleanup -v
```

---

## 📖 Commands

### `homelab status`

Display a dashboard of all your homelab containers with their current status and uptime.

```bash
homelab status
```

**Output:**
```
╦ ╦┌─┐┌┬┐┌─┐┬  ┌─┐┌┐   ╔═╗┌┬┐┌─┐┌┬┐┬ ┬┌─┐
╠═╣│ ││││├┤ │  ├─┤├┴┐  ╚═╗ │ ├─┤ │ │ │└─┐
╩ ╩└─┘┴ ┴└─┘┴─┘┴ ┴└─┘  ╚═╝ ┴ ┴ ┴ ┴ └─┘└─┘

╭───────────┬─────────┬─────────╮
│ Container │ Status  │ Uptime  │
├───────────┼─────────┼─────────┤
│ adguard   │ Running │ 3d 5h   │
│ wireguard │ Running │ 3d 5h   │
│ grafana   │ Stopped │ N/A     │
╰───────────┴─────────┴─────────╯
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
- [.NET 8](https://dotnet.microsoft.com/)
- [Spectre.Console](https://spectreconsole.net/) - Beautiful terminal UI
- [Docker.DotNet](https://github.com/dotnet/Docker.DotNet) - Docker API SDK
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
│       ├── Services/           # Business logic
│       │   ├── Docker/         # Docker operations
│       │   ├── Configuration/  # Config management
│       │   └── Health/         # Health checks
│       ├── Models/             # Data models
│       └── Program.cs          # Entry point
├── config/                     # Configuration files
├── docs/                       # Documentation
└── scripts/                    # Helper scripts
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

Future enhancements:

- [ ] Real-time log following (`-f` flag)
- [ ] Service dependency management
- [ ] Health check alerting
- [ ] Automated backup scheduling
- [ ] Notification integrations (Slack, Discord)
- [ ] Container resource monitoring
- [ ] TUI (Terminal UI) mode
- [ ] Remote homelab management (SSH)

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
