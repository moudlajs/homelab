# 🚀 Phase 5 Implementation - Session Handoff

**Date:** 2025-12-27
**From:** Phase 1-4 Development Session (v1.0.0)
**To:** Phase 5 Implementation Session (v1.5.0)
**Status:** Ready to begin Phase 5

---

## 📋 Quick Start Instructions

**You are starting Phase 5 of the HomeLab CLI project.**

### Your Mission
Transform HomeLab CLI from a **generic Docker container manager** to a **HomeLab-aware orchestration tool** that understands specific services (AdGuard, WireGuard, Prometheus, Grafana).

### Immediate First Steps
1. **Read the master plan:** `docs/PHASE_5_MASTER_PLAN.md`
2. **Understand current architecture:** Review existing code structure below
3. **Start Day 1:** Environment setup & foundation
4. **Follow git workflow:** Feature branches + PRs for EVERYTHING

---

## 🎯 Project Context

### Repository Information
- **Location:** `/Users/danielczetner/Repos/homelab`
- **Remote:** `git@github.com:moudlajs/homelab.git`
- **Current Version:** v1.0.0 (released)
- **Target Version:** v1.5.0 (Phase 5 completion)
- **Current Branch:** `main` (clean, all changes committed)

### What's Been Built (v1.0.0)
**Phase 1-4 Complete:**
- ✅ Phase 1: Foundation & Status Dashboard
- ✅ Phase 2: Service Lifecycle Control (start/stop/restart)
- ✅ Phase 3: Configuration Management (backup/restore)
- ✅ Phase 4: Maintenance & Automation (logs/update/cleanup)

**Result:** Generic Docker container manager - works with ANY containers

### What You're Building (v1.5.0)
**Phase 5: HomeLab-Specific Service Integration**

**New Features:**
1. Service Discovery - Auto-detect homelab services from docker-compose
2. Health Monitoring - Service-specific health checks
3. VPN Management - WireGuard peer management with QR codes
4. DNS Integration - AdGuard statistics and control
5. Monitoring - Prometheus/Grafana integration
6. Remote Management - SSH to Mac Mini
7. Service Dependencies - Smart orchestration

**Result:** HomeLab mission control - understands YOUR specific services

---

## 🏗️ Current Architecture

### Project Structure
```
homelab/
├── src/
│   └── HomeLab.Cli/
│       ├── Commands/              # CLI commands
│       │   ├── StatusCommand.cs
│       │   ├── ServiceCommand.cs
│       │   ├── ConfigCommand.cs
│       │   ├── LogsCommand.cs
│       │   ├── UpdateCommand.cs
│       │   └── CleanupCommand.cs
│       │
│       ├── Services/              # Business logic
│       │   ├── Docker/
│       │   │   ├── IDockerService.cs
│       │   │   └── DockerService.cs
│       │   ├── Configuration/
│       │   │   ├── IConfigService.cs
│       │   │   └── ConfigService.cs
│       │   ├── Backup/
│       │   │   ├── IBackupService.cs
│       │   │   └── BackupService.cs
│       │   └── Health/
│       │       ├── IHealthCheckService.cs
│       │       └── HealthCheckService.cs
│       │
│       ├── Models/                # Data models
│       │   ├── ContainerInfo.cs
│       │   ├── HealthCheckResult.cs
│       │   └── SystemResourceInfo.cs
│       │
│       ├── Infrastructure/        # DI and setup
│       │   └── TypeRegistrar.cs
│       │
│       ├── Program.cs             # Entry point
│       └── HomeLab.Cli.csproj
│
├── config/                        # Configuration files
├── docs/                          # Documentation
│   ├── PHASE_5_MASTER_PLAN.md    # ⭐ READ THIS FIRST
│   └── PHASE_5_HANDOFF.md        # This file
├── README.md
└── HomeLab.sln
```

### Technology Stack
- **.NET 8** - Target framework
- **Spectre.Console** - Beautiful terminal UI
- **Spectre.Console.Cli** - Command routing
- **Docker.DotNet** - Docker API integration
- **YamlDotNet** - YAML parsing (already installed)

### Existing NuGet Packages
```xml
<PackageReference Include="Docker.DotNet" Version="3.125.15" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
<PackageReference Include="Spectre.Console" Version="0.49.1" />
<PackageReference Include="Spectre.Console.Cli" Version="0.49.1" />
<PackageReference Include="YamlDotNet" Version="16.2.1" />
```

### Dependency Injection Setup
Located in `Program.cs`:
```csharp
var services = new ServiceCollection();
services.AddSingleton<IDockerService, DockerService>();
services.AddSingleton<IConfigService, ConfigService>();
services.AddSingleton<IBackupService, BackupService>();
services.AddSingleton<IHealthCheckService, HealthCheckService>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
```

### Command Registration Pattern
```csharp
app.Configure(config =>
{
    config.SetApplicationName("homelab");
    config.AddCommand<StatusCommand>("status");
    config.AddCommand<ServiceCommand>("service");
    // etc...
});
```

---

## 🌿 Git Workflow (CRITICAL!)

### Feature Branch Strategy

**❌ NEVER commit directly to main**

**✅ ALWAYS use this workflow:**
```bash
# 1. Create feature branch
git checkout -b feature/service-discovery

# 2. Work on feature, commit regularly
git add .
git commit -m "feat: implement service discovery from compose files"

# 3. Push to GitHub
git push origin feature/service-discovery

# 4. Create Pull Request
gh pr create --title "Feature: Service Discovery" --body "Implements service discovery..."

# 5. After approval, merge and delete branch
gh pr merge --squash
git branch -d feature/service-discovery
```

### Branch Naming Convention
```
feature/service-discovery       # New features
feature/vpn-management
feature/dns-integration
fix/health-check-timeout        # Bug fixes
refactor/docker-service         # Code improvements
docs/phase5-readme              # Documentation
```

### Commit Message Convention
```
feat: add service discovery from compose files
fix: resolve health check timeout issue
refactor: extract HTTP client to base class
docs: add Phase 5 implementation guide
test: add unit tests for AdGuardService
chore: update NuGet packages
```

---

## 📦 Phase 5 Implementation Plan

### Overview
**Timeline:** Quality over speed (don't rush, aim for 5-7 days)
**Strategy:** Feature branches for each major component
**Testing:** Real Docker services on MacBook, mock clients for unit tests

### Day 1: Environment Setup & Foundation
**Branch:** `feature/phase5-foundation`

**Tasks:**
- [ ] Setup mock homelab (docker-compose with all services)
- [ ] Install NuGet packages (System.Net.Http.Json, SSH.NET, QRCoder, Moq, xunit, FluentAssertions)
- [ ] Create IServiceClient abstraction
- [ ] Create ServiceClientFactory (real/mock toggle)
- [ ] Add configuration system (config/homelab-cli.yaml)

**Deliverables:**
- Mock homelab running on MacBook (AdGuard, WireGuard, Prometheus, Grafana)
- Abstraction layer in place
- Config system working

### Day 2: Service Discovery & Health Checks
**Branch:** `feature/service-discovery-health`

**Tasks:**
- [ ] Create IServiceDiscoveryService
- [ ] Implement ComposeFileParser (YamlDotNet)
- [ ] Parse docker-compose.yml → ServiceDefinition objects
- [ ] Create IHealthCheckService interface
- [ ] Implement service-specific health checks
- [ ] Create HealthCheckOrchestrator

**Deliverables:**
- Can discover all services from docker-compose
- Health checks working for each service type
- `homelab status` shows service health 🟢🟡🔴

### Day 3: VPN Management (WireGuard)
**Branch:** `feature/vpn-management`

**Tasks:**
- [ ] Create IWireGuardClient interface
- [ ] Implement WireGuardClient (parse config files)
- [ ] Implement MockWireGuardClient
- [ ] Create VpnCommand with subcommands
- [ ] Implement `homelab vpn status` (list peers, bandwidth)
- [ ] Implement `homelab vpn add-peer <name>` (generate config)
- [ ] Implement `homelab vpn remove-peer <name>`
- [ ] Generate QR codes for mobile peers (QRCoder)

**Deliverables:**
- `homelab vpn status` - shows all peers
- `homelab vpn add-peer "danny-phone"` - generates config + QR
- `homelab vpn remove-peer "danny-phone"` - removes peer

### Day 4: DNS & Monitoring Integration
**Branch:** `feature/dns-monitoring`

**DNS (AdGuard) Tasks:**
- [ ] Create IAdGuardClient interface
- [ ] Implement AdGuardClient (AdGuard Home API)
- [ ] Implement MockAdGuardClient
- [ ] Create DnsCommand
- [ ] Implement `homelab dns stats` (queries, blocks, %)
- [ ] Implement `homelab dns blocked` (top blocked domains)

**Monitoring (Prometheus/Grafana) Tasks:**
- [ ] Create IPrometheusClient + IGrafanaClient
- [ ] Implement real + mock clients
- [ ] Create MonitorCommand
- [ ] Implement `homelab monitor alerts`
- [ ] Implement `homelab monitor targets`
- [ ] Implement `homelab monitor dashboard`

**Deliverables:**
- Full DNS statistics from CLI
- Monitoring integration working
- Quick access to dashboards

### Day 5: Enhanced Status & Dependencies
**Branch:** `feature/enhanced-status`

**Tasks:**
- [ ] Create ServiceDependencyGraph
- [ ] Define dependencies (VPN → DNS, Monitor → All)
- [ ] Implement smart startup order
- [ ] Enhance `homelab status` with service-specific metrics
- [ ] Add dependency visualization
- [ ] Add interactive menu
- [ ] Add `--watch` flag for live updates

**Deliverables:**
- Beautiful enhanced status dashboard
- Service dependency awareness
- Interactive status command

### Day 6: Remote Management (SSH)
**Branch:** `feature/remote-management`

**Tasks:**
- [ ] Create ISshService interface
- [ ] Implement SshDockerService (SSH.NET)
- [ ] Create RemoteCommand
- [ ] Implement `homelab remote connect <ip>`
- [ ] Implement `homelab remote status`
- [ ] Implement `homelab remote sync`
- [ ] Store connection profiles (~/.homelab/remotes.yaml)
- [ ] Add `--remote` flag to all commands

**Deliverables:**
- Can execute commands on Mac Mini via SSH
- Configuration sync working
- All commands work remotely

### Day 7: Polish, Testing & Release
**Branch:** `release/v1.5.0`

**Tasks:**
- [ ] Write unit tests for all new services
- [ ] Integration tests with real services
- [ ] Update README with Phase 5 features
- [ ] Create CHANGELOG for v1.5.0
- [ ] Run code review on all PRs
- [ ] Fix any issues found
- [ ] Merge all feature branches
- [ ] Tag v1.5.0
- [ ] Build release binary
- [ ] Create GitHub release

**Deliverables:**
- v1.5.0 released
- All tests passing
- Documentation complete

---

## 🛠️ Development Environment Setup

### Mock HomeLab Docker Compose

**User will manually create:** `~/Projects/homelab-mock/docker-compose.yml`

**Tell user to run:**
```bash
mkdir -p ~/Projects/homelab-mock
cd ~/Projects/homelab-mock
# Create docker-compose.yml (content in PHASE_5_MASTER_PLAN.md)
docker-compose up -d
```

**Services included:**
- AdGuard Home (DNS & Ad blocking) - Port 3000, 53
- WireGuard (VPN) - Port 51820
- Prometheus (Metrics) - Port 9090
- Grafana (Dashboards) - Port 3001
- Node Exporter (System metrics) - Port 9100

### Configuration File

**Location:** `config/homelab-cli.yaml`

**Structure:**
```yaml
development:
  use_mock_services: false  # false = real Docker, true = mocks
  docker_host: "unix:///var/run/docker.sock"

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

remote:
  mac_mini:
    host: "192.168.1.100"
    user: "your-username"
    docker_host: "unix:///var/run/docker.sock"
```

### NuGet Packages to Install

**Tell user to run (or install yourself if possible):**
```bash
cd /Users/danielczetner/Repos/homelab/src/HomeLab.Cli

dotnet add package System.Net.Http.Json      # HTTP API calls
dotnet add package SSH.NET                   # SSH for remote management
dotnet add package QRCoder                   # QR codes for VPN configs
dotnet add package Moq                       # Mocking framework for tests
dotnet add package xunit                     # Testing framework
dotnet add package FluentAssertions          # Better test assertions
```

---

## 📋 Success Criteria

**Phase 5 is complete when:**

1. ✅ All services auto-discovered from docker-compose
2. ✅ Health checks working for all service types
3. ✅ Can manage VPN peers (add/remove/list)
4. ✅ Can view DNS statistics and alerts
5. ✅ Can access monitoring dashboards from CLI
6. ✅ Can manage Mac Mini remotely from MacBook
7. ✅ Service dependencies respected
8. ✅ All features tested (unit + integration)
9. ✅ Documentation complete
10. ✅ All changes went through PR + code review
11. ✅ v1.5.0 released on GitHub

---

## 🚨 Important Notes

### User Preferences
- **Quality over speed** - Don't rush, focus on doing it right
- **Manual installation OK** - If user needs to manually install services (docker-compose, etc.), just tell them
- **Real services preferred** - Develop against real Docker services, not mocks (mocks only for tests)
- **Feature branches required** - EVERY feature goes through PR workflow
- **No direct commits to main** - User learned this lesson in Phase 1-4

### Architecture Patterns to Follow
1. **Interface-based design** - Always create interface first, then implementation
2. **Dependency injection** - Register all services in Program.cs
3. **Async/await** - All I/O operations are async
4. **Clean separation** - Commands → Services → Models → Infrastructure
5. **Factory pattern** - Use factories for swapping real/mock implementations

### Known Issues from Phase 1-4
1. **Trimmed builds fail** - Don't use PublishTrimmed with Spectre.Console.Cli (uses reflection)
2. **MultiplexedStream** - Docker logs use `ReadOutputToEndAsync`, not standard stream methods
3. **ulong not long** - Docker API returns ulong for space values
4. **CancellationToken required** - ExecuteAsync must accept CancellationToken parameter

---

## 🎯 Your First Actions

**Start here:**

1. **Read the master plan:**
   ```bash
   # Read this file to understand full scope
   cat docs/PHASE_5_MASTER_PLAN.md
   ```

2. **Review existing architecture:**
   ```bash
   # Understand current code structure
   ls -la src/HomeLab.Cli/Commands/
   ls -la src/HomeLab.Cli/Services/
   cat src/HomeLab.Cli/Program.cs
   ```

3. **Tell user what to install:**
   - Provide docker-compose.yml content
   - List NuGet packages to install
   - Confirm they're ready to proceed

4. **Create first feature branch:**
   ```bash
   git checkout -b feature/phase5-foundation
   ```

5. **Start Day 1 implementation:**
   - Create abstraction layer (IServiceClient)
   - Create ServiceClientFactory
   - Add configuration system
   - Register new services in DI

---

## 📧 Questions?

If anything is unclear, the user (Daniel) prefers:
- Direct questions about implementation choices
- Being told what to install manually (docker services, etc.)
- Focus on quality over speed
- Proper git workflow (feature branches + PRs)

**User's communication style:** Fast typing with typos, but clear intent. Read between the lines.

---

## 🚀 Ready to Start!

You have everything you need:
- ✅ Clear mission (transform to HomeLab-aware tool)
- ✅ Detailed plan (docs/PHASE_5_MASTER_PLAN.md)
- ✅ Current architecture understanding
- ✅ Git workflow requirements
- ✅ User preferences

**Let's build v1.5.0!** 🔥

Focus on quality, follow the feature branch workflow, and create something amazing.

---

**Generated:** 2025-12-27
**Session:** Phase 1-4 Complete → Phase 5 Handoff
**Good luck! 🚀**
