# HomeLab CLI - Quick Start Guide

## 🎯 Understanding the System

### What Actually Works RIGHT NOW

The HomeLab CLI has **two modes**:

1. **Mock Mode** (`use_mock_services: true`) - Testing without real services
2. **Real Mode** (`use_mock_services: false`) - Connects to actual homelab services

**Current Status:** v1.1.0 is feature-complete with all Phase 5 commands implemented.

---

## 🏗️ Architecture Overview

### Configuration System

**Main Config File:** `config/homelab-cli.yaml`

```yaml
development:
  use_mock_services: false              # true = fake data, false = real services
  docker_host: "unix:///var/run/docker.sock"
  compose_file: "~/Projects/homelab-mock/docker-compose.yml"

services:
  adguard:
    url: "http://localhost:3000"
    username: "admin"
    password: "admin"
  wireguard:
    config_path: "~/Projects/homelab-mock/data/wireguard"
  prometheus:
    url: "http://localhost:9090"
  grafana:
    url: "http://localhost:3001"
    username: "admin"
    password: "admin"
```

**Remote Profiles:** `~/.homelab/remotes.yaml` (created by `homelab remote connect`)

### How Services Are Switched

```
use_mock_services: true
  ↓
ServiceClientFactory
  ↓
MockAdGuardClient → Returns fake data (no API calls)
MockWireGuardClient → Returns fake peers
MockPrometheusClient → Returns fake alerts
MockGrafanaClient → Returns fake dashboards

use_mock_services: false
  ↓
ServiceClientFactory
  ↓
AdGuardClient → HTTP calls to http://localhost:3000
WireGuardClient → Reads/writes ~/Projects/homelab-mock/data/wireguard
PrometheusClient → HTTP calls to http://localhost:9090
GrafanaClient → HTTP calls to http://localhost:3001
```

---

## 📋 Command Status Matrix

| Command | Status | Dependencies | Works in Mock Mode? |
|---------|--------|--------------|---------------------|
| `homelab status` | ✅ Complete | Docker running | Yes (fake data) |
| `homelab status --watch` | ✅ Complete | Docker running | Yes |
| `homelab status --show-dependencies` | ✅ Complete | Docker running | Yes |
| `homelab service start/stop/restart` | ✅ Complete | Docker running | Partial (Docker only) |
| `homelab logs <container>` | ✅ Complete | Docker running | No (needs real containers) |
| `homelab vpn status` | ✅ Complete | WireGuard config dir | Yes (fake peers) |
| `homelab vpn add-peer <name>` | ✅ Complete | WireGuard config dir | Yes (creates files) |
| `homelab vpn remove-peer <name>` | ✅ Complete | WireGuard config dir | Yes |
| `homelab dns stats` | ✅ Complete | AdGuard Home running | Yes (fake stats) |
| `homelab dns blocked` | ✅ Complete | AdGuard Home running | Yes (fake domains) |
| `homelab monitor alerts` | ✅ Complete | Prometheus running | Yes (fake alerts) |
| `homelab monitor targets` | ✅ Complete | Prometheus running | Yes (fake targets) |
| `homelab monitor dashboard` | ✅ Complete | Grafana running | Yes (fake dashboards) |
| `homelab remote connect` | ✅ Complete | SSH access | No (needs real SSH) |
| `homelab remote list` | ✅ Complete | None | No |
| `homelab remote status` | ✅ Complete | SSH access | No |
| `homelab remote sync` | ✅ Complete | SSH access | No |
| `homelab config view/edit/backup` | ✅ Complete | None | Yes |
| `homelab update <image>` | ✅ Complete | Docker running | No |
| `homelab cleanup` | ✅ Complete | Docker running | No |

**Legend:**
- ✅ Complete = Fully implemented
- Yes = Works with `use_mock_services: true`
- No = Requires real services
- Partial = Some features work

---

## 🚀 Getting Started

### Option 1: Development/Testing (Mock Mode)

**Use this when:** You want to test the CLI without running actual homelab services.

#### Step 1: Build the CLI

```bash
cd ~/Repos/homelab

# Build release version
dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained \
  -o ./bin/release \
  /p:PublishSingleFile=true

# Make executable
chmod +x ./bin/release/homelab

# Optional: Add to PATH
sudo cp ./bin/release/homelab /usr/local/bin/
```

#### Step 2: Configure Mock Mode

Edit `config/homelab-cli.yaml`:

```yaml
development:
  use_mock_services: true  # ← Set to true
  docker_host: "unix:///var/run/docker.sock"
  compose_file: "~/Repos/homelab/docker-compose.yml"  # ← Use repo's compose file
```

#### Step 3: Create a Test docker-compose.yml

Create `~/Repos/homelab/docker-compose.yml`:

```yaml
version: '3.8'

services:
  adguard:
    image: adguard/adguardhome:latest
    container_name: homelab_adguard
    ports:
      - "3000:3000"
      - "53:53/tcp"
      - "53:53/udp"

  wireguard:
    image: linuxserver/wireguard:latest
    container_name: homelab_wireguard
    ports:
      - "51820:51820/udp"

  prometheus:
    image: prom/prometheus:latest
    container_name: homelab_prometheus
    ports:
      - "9090:9090"

  grafana:
    image: grafana/grafana:latest
    container_name: homelab_grafana
    ports:
      - "3001:3000"

  node-exporter:
    image: prom/node-exporter:latest
    container_name: homelab_node_exporter
    ports:
      - "9100:9100"
```

#### Step 4: Start Mock Containers (Optional)

If you want to test Docker commands:

```bash
cd ~/Repos/homelab
docker-compose up -d
```

#### Step 5: Test Mock Commands

```bash
# These will return FAKE DATA (no services needed)
homelab status                    # Shows fake service health
homelab vpn status                # Shows fake VPN peers
homelab dns stats                 # Shows fake DNS statistics
homelab monitor alerts            # Shows fake Prometheus alerts
homelab monitor dashboard         # Lists fake Grafana dashboards

# These need Docker running
homelab service start adguard     # Starts actual container
homelab logs adguard              # Shows real container logs
homelab status                    # Shows real Docker status + fake service health
```

**What Happens in Mock Mode:**
- Service-specific commands (VPN, DNS, Monitoring) return **fake data**
- Docker commands (start/stop/logs) work with **real Docker**
- Good for testing UI and command structure
- No need to configure AdGuard/Prometheus/Grafana

---

### Option 2: Production (Real Homelab)

**Use this when:** You have a real homelab with services running.

#### Step 1: Install the CLI

```bash
# Download from GitHub release
curl -L https://github.com/moudlajs/homelab/releases/latest/download/homelab -o homelab
chmod +x homelab
sudo mv homelab /usr/local/bin/

# OR build from source (same as Option 1 Step 1)
```

#### Step 2: Configure Real Mode

Edit `config/homelab-cli.yaml`:

```yaml
development:
  use_mock_services: false  # ← Set to false
  docker_host: "unix:///var/run/docker.sock"
  compose_file: "~/homelab/docker-compose.yml"  # ← Your actual compose file

services:
  adguard:
    url: "http://192.168.1.100:3000"  # ← Your AdGuard Home URL
    username: "admin"                  # ← Your username
    password: "your-password"          # ← Your password

  wireguard:
    config_path: "/etc/wireguard"      # ← Your WireGuard config directory

  prometheus:
    url: "http://192.168.1.100:9090"   # ← Your Prometheus URL

  grafana:
    url: "http://192.168.1.100:3001"   # ← Your Grafana URL
    username: "admin"
    password: "your-password"
```

#### Step 3: Verify Services Are Running

```bash
# Check AdGuard Home
curl http://192.168.1.100:3000/control/status

# Check Prometheus
curl http://192.168.1.100:9090/-/healthy

# Check Grafana
curl http://192.168.1.100:3001/api/health

# Check Docker
docker ps
```

#### Step 4: Test Real Commands

```bash
# Status with real health checks
homelab status

# Real VPN management (reads/writes config files)
homelab vpn status
homelab vpn add-peer danny-phone

# Real DNS statistics from AdGuard Home
homelab dns stats
homelab dns blocked -n 20

# Real monitoring data
homelab monitor alerts
homelab monitor targets
homelab monitor dashboard

# Service control
homelab service restart adguard
homelab logs grafana -n 100
```

**What Happens in Real Mode:**
- Service commands make **actual API calls** to your homelab
- Requires services to be running and accessible
- Returns **real data** from your homelab
- Configuration changes (VPN peers, etc.) are **permanent**

---

### Option 3: Remote Homelab (SSH)

**Use this when:** Your homelab is on a remote Mac Mini or server.

#### Step 1: Set Up SSH Access

```bash
# Generate SSH key if you don't have one
ssh-keygen -t ed25519 -C "homelab-cli"

# Copy to remote (Mac Mini)
ssh-copy-id admin@192.168.1.100

# Test connection
ssh admin@192.168.1.100 "docker ps"
```

#### Step 2: Add Remote Connection

```bash
homelab remote connect mac-mini 192.168.1.100 \
  -u admin \
  -k ~/.ssh/id_ed25519 \
  --compose-file ~/homelab/docker-compose.yml \
  --default

# Output:
# Testing SSH connection to 192.168.1.100:22...
# ✓ SSH connection successful
# ✓ Docker is running on remote host
# System Info: 8 CPUs, 16.00 GB RAM, Docker version 24.0.7
# ✓ Connection 'mac-mini' saved and set as default
```

#### Step 3: Check Remote Status

```bash
# Check remote homelab status
homelab remote status

# List all remote connections
homelab remote list

# Output:
# ┌─────────┬──────────────────┬──────┬──────────┬─────────────────────┬─────────┐
# │ Name    │ Host             │ Port │ Username │ Last Connected      │ Default │
# ├─────────┼──────────────────┼──────┼──────────┼─────────────────────┼─────────┤
# │ mac-mini│ 192.168.1.100    │ 22   │ admin    │ 2025-01-15 10:30:00 │ ⭐      │
# └─────────┴──────────────────┴──────┴──────────┴─────────────────────┴─────────┘
```

#### Step 4: Sync Configuration

```bash
# Push local docker-compose.yml to remote
homelab remote sync --push

# Pull remote docker-compose.yml to local
homelab remote sync --pull

# Custom file paths
homelab remote sync --push \
  --local-file docker-compose.prod.yml \
  --remote-file /opt/homelab/docker-compose.yml
```

#### Step 5: Use Remote Operations

**Note:** Currently, the CLI doesn't automatically execute commands on remote.
You need to:

1. SSH into the remote and run commands there, OR
2. Use `homelab remote status` to check remote, OR
3. Future enhancement: Add `--remote <name>` flag to all commands

---

## 🧪 Testing Workflow

### Quick Test (5 minutes)

```bash
# 1. Build CLI
cd ~/Repos/homelab
dotnet build

# 2. Set mock mode
# Edit config/homelab-cli.yaml: use_mock_services: true

# 3. Test commands
dotnet run --project src/HomeLab.Cli -- status
dotnet run --project src/HomeLab.Cli -- vpn status
dotnet run --project src/HomeLab.Cli -- dns stats
dotnet run --project src/HomeLab.Cli -- monitor alerts

# Expected: All commands return fake data successfully
```

### Full Test (30 minutes)

```bash
# 1. Start mock homelab
cd ~/Projects/homelab-mock
docker-compose up -d

# 2. Set real mode
# Edit config/homelab-cli.yaml: use_mock_services: false

# 3. Wait for services to start (30 seconds)
sleep 30

# 4. Test status
homelab status
homelab status --watch --interval 5  # Ctrl+C to stop

# 5. Test service control
homelab service stop grafana
homelab service start grafana
homelab service restart prometheus

# 6. Test logs
homelab logs adguard -n 50

# 7. Test VPN (if wireguard config exists)
homelab vpn status
homelab vpn add-peer test-device
homelab vpn remove-peer test-device

# 8. Test DNS (if AdGuard is configured and running)
homelab dns stats
homelab dns blocked -n 10

# 9. Test monitoring (if Prometheus/Grafana are running)
homelab monitor alerts
homelab monitor targets
homelab monitor dashboard

# 10. Cleanup
homelab cleanup
```

---

## ❓ Troubleshooting

### "Config file not found"

```bash
# Ensure you're in the project directory
cd ~/Repos/homelab

# Check config file exists
ls -la config/homelab-cli.yaml

# The CLI looks for config relative to current working directory
# Always run from the project root, OR
# Copy config/homelab-cli.yaml to your working directory
```

### "Docker socket not found"

```bash
# Ensure Docker is running
docker ps

# Check socket permissions
ls -la /var/run/docker.sock

# Try with OrbStack instead of Docker Desktop
# OrbStack uses the same socket path
```

### "Service not found"

```bash
# Check docker-compose.yml path in config
cat config/homelab-cli.yaml | grep compose_file

# Ensure compose file exists
ls -la ~/Projects/homelab-mock/docker-compose.yml

# Container names must be prefixed with 'homelab_'
# Example: homelab_adguard, homelab_wireguard
docker ps --format "{{.Names}}"
```

### "Connection refused" (AdGuard/Prometheus/Grafana)

```bash
# Ensure services are running
docker ps

# Check service URLs in config
cat config/homelab-cli.yaml | grep -A 3 "services:"

# Test URLs manually
curl http://localhost:3000/control/status  # AdGuard
curl http://localhost:9090/-/healthy       # Prometheus
curl http://localhost:3001/api/health      # Grafana

# Set mock mode if services aren't running
# Edit config: use_mock_services: true
```

### "SSH connection failed"

```bash
# Test SSH manually
ssh -i ~/.ssh/id_rsa admin@192.168.1.100

# Check key file permissions
chmod 600 ~/.ssh/id_rsa

# Try with password auth (will prompt)
homelab remote connect mac-mini 192.168.1.100 -u admin

# Check remote Docker
ssh admin@192.168.1.100 "docker ps"
```

---

## 🎓 Understanding Mock vs Real

### Mock Mode Benefits

✅ **Fast development** - No service dependencies
✅ **Predictable data** - Same fake data every time
✅ **No configuration** - Works out of the box
✅ **Safe testing** - Can't break anything
✅ **UI testing** - Perfect for testing Spectre.Console output

### Mock Mode Limitations

❌ **Fake data only** - Not connected to real services
❌ **No persistence** - Changes don't affect real homelab
❌ **Limited realism** - May not catch integration issues
❌ **Docker still needed** - For service start/stop/logs

### Real Mode Benefits

✅ **Actual data** - Real statistics from your homelab
✅ **Real operations** - Changes are permanent
✅ **Integration testing** - Tests real API connections
✅ **Production ready** - What you use day-to-day

### Real Mode Requirements

⚠️ **Services must be running** - AdGuard, Prometheus, Grafana
⚠️ **Correct URLs** - Must match your network
⚠️ **Valid credentials** - Username/password for APIs
⚠️ **Network access** - CLI must reach services

---

## 📊 What's Next?

After getting familiar with the CLI, consider:

1. **Create your production config** - Set up real homelab services
2. **Write unit tests** - Test coverage is currently 0%
3. **Add follow mode to logs** - `homelab logs -f` for live tail
4. **Create TUI mode** - Terminal UI for interactive management
5. **Add notifications** - Slack/Discord webhooks for alerts
6. **Performance monitoring** - Track resource usage over time

---

## 🆘 Getting Help

- **Documentation**: [README.md](../README.md)
- **Changelog**: [CHANGELOG.md](../CHANGELOG.md)
- **Phase 5 Details**: [PHASE_5_COMPLETE.md](PHASE_5_COMPLETE.md)
- **Issues**: [GitHub Issues](https://github.com/moudlajs/homelab/issues)

---

**Current Version:** v1.1.0
**Last Updated:** January 2025
**Status:** ✅ All Phase 5 features complete
