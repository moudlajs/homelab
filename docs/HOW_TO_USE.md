# How to Use HomeLab CLI - Simple Guide

**For:** Daniel (and anyone confused about this tool 😅)

---

## 🤔 What is This?

`homelab` is a command-line tool to manage your homelab services (Docker containers). Think of it like a remote control for your servers.

---

## 🔧 The Problem You Had

You were trying to run `homelab dns stats` but it said **"Unknown command 'dns'"**.

### Why It Didn't Work

Your installed `homelab` binary was **OLD** (v1.0.0) and didn't have the new Phase 5 features (vpn, dns, monitor, remote).

### What I Fixed

1. Rebuilt the CLI with ALL Phase 5 features
2. Installed the new binary to `/usr/local/bin/homelab`
3. Now you have ALL commands available!

---

## 📝 Two Ways to Run Commands

### Option 1: Development (Using Source Code)

**When:** You're developing/testing and want to run from source code

```bash
cd ~/Repos/homelab
dotnet run --project src/HomeLab.Cli -- <command>

# Examples:
dotnet run --project src/HomeLab.Cli -- status
dotnet run --project src/HomeLab.Cli -- dns stats
dotnet run --project src/HomeLab.Cli -- vpn status
```

**Pros:** Always uses latest code changes
**Cons:** Slow to start (has to compile), long command

### Option 2: Production (Using Installed Binary)

**When:** Daily use, faster, shorter commands

```bash
homelab <command>

# Examples:
homelab status
homelab dns stats
homelab vpn status
```

**Pros:** Fast, short commands
**Cons:** Need to rebuild and reinstall after code changes

---

## 🔄 When to Rebuild and Reinstall

**You need to rebuild when:**
- You change any code
- You add new features
- You want the latest version

**How to rebuild and install:**

```bash
cd ~/Repos/homelab

# 1. Build (takes ~5 seconds)
dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained \
  -o ./bin/release \
  /p:PublishSingleFile=true

# 2. Install (replaces old version)
sudo cp ./bin/release/HomeLab.Cli /usr/local/bin/homelab

# 3. Verify it works
homelab --help
```

**Pro tip:** Create an alias to make this easier:

```bash
# Add to your ~/.zshrc:
alias homelab-rebuild='cd ~/Repos/homelab && dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj -c Release -r osx-arm64 --self-contained -o ./bin/release /p:PublishSingleFile=true && sudo cp ./bin/release/HomeLab.Cli /usr/local/bin/homelab && echo "✅ HomeLab CLI rebuilt and installed!"'

# Then just run:
homelab-rebuild
```

---

## 🎮 Available Commands

### ✅ NOW WORKING (All Phase 5 Features!)

```bash
# Status Dashboard
homelab status                    # Show all services
homelab status --watch            # Live updates
homelab status --show-dependencies  # Dependency graph

# Service Control
homelab service start adguard     # Start a service
homelab service stop grafana      # Stop a service
homelab service restart prometheus  # Restart a service

# VPN Management (NEW!)
homelab vpn status                # List VPN peers
homelab vpn add-peer danny-phone  # Add new peer with QR code
homelab vpn remove-peer old-device  # Remove peer

# DNS Management (NEW!)
homelab dns stats                 # DNS statistics
homelab dns blocked -n 20         # Top 20 blocked domains

# Monitoring (NEW!)
homelab monitor alerts            # Prometheus alerts
homelab monitor targets           # Scrape targets
homelab monitor dashboard         # List Grafana dashboards

# Remote Management (NEW!)
homelab remote connect mac-mini 192.168.1.100 -u admin -k ~/.ssh/id_rsa
homelab remote list               # List connections
homelab remote status             # Check remote status

# Configuration
homelab config view               # View docker-compose.yml
homelab config backup             # Create backup

# Container Logs
homelab logs adguard -n 100       # View logs

# Maintenance
homelab update nginx              # Update image
homelab cleanup                   # Clean up resources
```

---

## 🎯 Mock vs Real Mode

### Understanding the Two Modes

The CLI has **two modes** controlled by `config/homelab-cli.yaml`:

#### Mock Mode (`use_mock_services: true`)

**What it does:** Returns FAKE data for testing

**When to use:**
- Testing the CLI without running services
- Developing new features
- Demonstrating the UI

**Example:**
```yaml
# config/homelab-cli.yaml
development:
  use_mock_services: true  # ← FAKE data
```

```bash
homelab dns stats
# Shows fake statistics like:
# Total Queries: 98,153
# Blocked: 13,935 (14.20%)
```

#### Real Mode (`use_mock_services: false`)

**What it does:** Makes REAL API calls to your services

**When to use:**
- Daily homelab management
- Actual monitoring
- Real VPN peer management

**Requires:**
- Services must be running (AdGuard, Prometheus, etc.)
- Correct URLs in config
- Valid credentials

**Example:**
```yaml
# config/homelab-cli.yaml
development:
  use_mock_services: false  # ← REAL data

services:
  adguard:
    url: "http://192.168.1.100:3000"  # Your actual IP
    username: "admin"
    password: "your-password"
```

```bash
homelab dns stats
# Shows REAL statistics from your AdGuard Home
```

### How to Switch Modes

**Option 1: Edit Config File**

```bash
# Open config
nano ~/Repos/homelab/config/homelab-cli.yaml

# Change this line:
use_mock_services: true   # or false

# Save and run commands
homelab dns stats
```

**Option 2: Use Environment Variable (Future Feature)**

Not implemented yet, but could add:
```bash
export HOMELAB_MOCK=true
homelab dns stats  # Uses mock mode
```

---

## ⚠️ Common Errors and Fixes

### Error: "Unknown command 'dns'"

**Problem:** Old binary installed (v1.0.0)
**Solution:** Rebuild and reinstall (see above)

```bash
homelab --help  # Check if you see vpn, dns, monitor, remote
# If not, rebuild!
```

### Error: "Container 'wireguard' not found"

**Problem:** Container name is wrong
**Solution:** Containers MUST be named `homelab_<service>`

```yaml
# docker-compose.yml
services:
  wireguard:
    container_name: homelab_wireguard  # ✅ Correct
    # NOT: wireguard                   # ❌ Wrong
```

### Error: "AdGuard Home is not healthy: Connection refused"

**Problem:** AdGuard is not running OR wrong URL
**Solutions:**

1. **Check if it's running:**
   ```bash
   docker ps | grep adguard
   curl http://localhost:3000/control/status
   ```

2. **Start it:**
   ```bash
   docker-compose up -d adguard
   ```

3. **Use mock mode instead:**
   ```bash
   # Edit config: use_mock_services: true
   homelab dns stats  # Now works with fake data
   ```

### Error: "Config file not found"

**Problem:** Running from wrong directory
**Solution:** Always run from project root

```bash
cd ~/Repos/homelab
homelab status  # ✅ Works

cd ~/Desktop
homelab status  # ❌ Fails (can't find config)
```

---

## 🚀 Quick Start Workflow

### For Testing (5 minutes)

```bash
# 1. Go to project
cd ~/Repos/homelab

# 2. Enable mock mode
nano config/homelab-cli.yaml
# Set: use_mock_services: true

# 3. Test commands (returns fake data)
homelab status
homelab vpn status
homelab dns stats
homelab monitor alerts
```

### For Real Use (Mac Mini)

```bash
# 1. Make sure Mac Mini is running services
ssh admin@192.168.1.100 "docker ps"

# 2. Configure real mode
cd ~/Repos/homelab
nano config/homelab-cli.yaml
# Set: use_mock_services: false
# Update URLs to Mac Mini IP (192.168.1.100)

# 3. Add remote connection
homelab remote connect mac-mini 192.168.1.100 \
  -u admin \
  -k ~/.ssh/id_rsa \
  --default

# 4. Check remote status
homelab remote status

# 5. Use commands
homelab status
homelab dns stats
homelab vpn add-peer laptop
```

---

## 💡 Pro Tips

### 1. Create Aliases

```bash
# Add to ~/.zshrc or ~/.bashrc:
alias hl='homelab'
alias hls='homelab status'
alias hlr='homelab-rebuild'

# Then use:
hl status
hls
```

### 2. Use Tab Completion

*(Not implemented yet, but would be nice!)*

### 3. Check Version

```bash
# Currently no --version flag
# Check last rebuild date:
ls -lh /usr/local/bin/homelab
```

### 4. View Help Anytime

```bash
homelab --help           # All commands
homelab vpn --help       # VPN commands
homelab dns --help       # DNS commands
homelab monitor --help   # Monitor commands
homelab remote --help    # Remote commands
```

---

## 🎓 Understanding the Difference

### `dotnet run` vs `homelab`

| Aspect | `dotnet run --project src/HomeLab.Cli -- <cmd>` | `homelab <cmd>` |
|--------|------------------------------------------------|-----------------|
| **Speed** | Slow (compiles first) | Fast (pre-compiled) |
| **Use case** | Development/testing | Daily use |
| **Updates** | Always latest code | Needs rebuild |
| **Command length** | Very long | Short |
| **When to use** | When changing code | After installing |

### Think of it like:

- `dotnet run` = Interpreted mode (like Python)
- `homelab` = Compiled binary (like a .app)

---

## 📚 Next Steps

1. **Try mock mode** to learn commands without breaking anything
2. **Start real services** on Mac Mini or locally
3. **Switch to real mode** and test with actual data
4. **Set up SSH** for remote management
5. **Create aliases** for faster commands

---

## 🆘 Still Confused?

**Read these in order:**

1. `docs/HOW_TO_USE.md` ← You are here!
2. `docs/QUICK_START.md` ← Detailed technical guide
3. `docs/TESTING_REPORT.md` ← What was tested
4. `README.md` ← Full documentation

**Or just ask:** "How do I [do something]?"

---

**Last Updated:** December 27, 2025
**CLI Version:** v1.1.0 (Phase 5 Complete)
**Status:** ✅ All commands working!
