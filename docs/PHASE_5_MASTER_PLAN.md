# 🚀 HomeLab CLI - Phase 5 Master Plan

**Status:** 📋 AWAITING APPROVAL
**Timeline:** 5-7 DAYS (aggressive, achievable)
**Environment:** MacBook (mock homelab) → Mac Mini (production)

---

## 📊 Current State

### ✅ What We Have (v1.0.0)
- **Phase 1:** Foundation & Status Dashboard
- **Phase 2:** Service Lifecycle Control (start/stop/restart)
- **Phase 3:** Configuration Management (backup/restore)
- **Phase 4:** Maintenance & Automation (logs/update/cleanup)

**Result:** Generic container manager - works with ANY Docker containers

### 🎯 What We're Building (v1.5.0)
- **Phase 5:** HomeLab-Specific Service Integration

**Result:** HomeLab-aware orchestration tool - understands YOUR specific services

---

## 🎯 Phase 5 Goals

Transform from **"Docker container manager"** to **"HomeLab mission control"**

### Core Features

1. **Service Discovery** - Auto-detect homelab services from docker-compose
2. **Health Monitoring** - Service-specific health checks (DNS, VPN, monitoring)
3. **Service Management** - VPN peer management, DNS stats, monitoring dashboards
4. **Smart Orchestration** - Understand service dependencies
5. **Remote Control** - Manage Mac Mini from MacBook (SSH tunneling)

---

## 🏗️ Architecture Overview

### New Components

```
HomeLab.Cli/
├── Services/
│   ├── Abstractions/          # Interface layer (IServiceClient)
│   │   ├── IAdGuardClient.cs
│   │   ├── IWireGuardClient.cs
│   │   ├── IPrometheusClient.cs
│   │   └── IGrafanaClient.cs
│   │
│   ├── ServiceDiscovery/      # Parse docker-compose, find services
│   │   ├── IServiceDiscoveryService.cs
│   │   └── ComposeFileParser.cs
│   │
│   ├── AdGuard/               # DNS & Ad-blocking
│   │   ├── AdGuardClient.cs           # Real API calls
│   │   ├── MockAdGuardClient.cs       # Fake data for tests
│   │   └── AdGuardService.cs          # Business logic
│   │
│   ├── WireGuard/             # VPN Management
│   │   ├── WireGuardClient.cs         # Real config file parsing
│   │   ├── MockWireGuardClient.cs     # Fake peers
│   │   └── WireGuardService.cs        # Peer management logic
│   │
│   ├── Prometheus/            # Metrics & Monitoring
│   │   ├── PrometheusClient.cs        # Real API calls
│   │   ├── MockPrometheusClient.cs    # Fake metrics
│   │   └── PrometheusService.cs       # Query logic
│   │
│   ├── Grafana/               # Dashboards
│   │   ├── GrafanaClient.cs
│   │   ├── MockGrafanaClient.cs
│   │   └── GrafanaService.cs
│   │
│   └── Remote/                # SSH to Mac Mini
│       ├── ISshService.cs
│       └── SshDockerService.cs
│
├── Commands/
│   ├── VpnCommand.cs          # homelab vpn [status|add-peer|remove-peer]
│   ├── DnsCommand.cs          # homelab dns [stats|blocked|update]
│   ├── MonitorCommand.cs      # homelab monitor [alerts|targets|dashboard]
│   └── RemoteCommand.cs       # homelab remote [connect|sync]
│
└── Models/
    ├── ServiceDefinition.cs   # Parsed from docker-compose
    ├── ServiceHealth.cs       # Health check results
    ├── VpnPeer.cs            # WireGuard peer info
    ├── DnsStats.cs           # AdGuard statistics
    └── AlertInfo.cs          # Prometheus alerts
```

---

## 📅 Implementation Timeline (5-7 Days)

### Day 1: Environment Setup & Foundation
**Goal:** Get mock homelab running, create abstraction layer

**Tasks:**
- [ ] Setup mock homelab (docker-compose with all services)
- [ ] Install NuGet packages (System.Net.Http.Json, SSH.NET, QRCoder)
- [ ] Create IServiceClient abstraction
- [ ] Create ServiceClientFactory (real/mock toggle)
- [ ] Add configuration system (config/homelab-cli.yaml)

**Deliverables:**
- Mock homelab running on MacBook (AdGuard, WireGuard, Prometheus, Grafana)
- Abstraction layer in place
- Config system working

**Branch:** `feature/phase5-foundation`

---

### Day 2: Service Discovery & Health Checks
**Goal:** Auto-discover services and check their health

**Tasks:**
- [ ] Create IServiceDiscoveryService
- [ ] Implement ComposeFileParser (YamlDotNet)
- [ ] Parse docker-compose.yml → ServiceDefinition objects
- [ ] Create IHealthCheckService interface
- [ ] Implement AdGuardHealthCheck (DNS query + API)
- [ ] Implement WireGuardHealthCheck (config file check)
- [ ] Implement PrometheusHealthCheck (API /health endpoint)
- [ ] Create HealthCheckOrchestrator (runs all checks)

**Deliverables:**
- Can discover all services from docker-compose
- Health checks working for each service type
- `homelab status` shows service health 🟢🟡🔴

**Branch:** `feature/service-discovery-health`

---

### Day 3: VPN Management (WireGuard)
**Goal:** Full WireGuard peer management

**Tasks:**
- [ ] Create IWireGuardClient interface
- [ ] Implement WireGuardClient (parse config files)
- [ ] Implement MockWireGuardClient
- [ ] Create VpnCommand with subcommands
- [ ] Implement `homelab vpn status` (list peers, bandwidth)
- [ ] Implement `homelab vpn add-peer <name>` (generate config)
- [ ] Implement `homelab vpn remove-peer <name>`
- [ ] Generate QR codes for mobile peers (QRCoder)
- [ ] Add VPN health to enhanced status

**Deliverables:**
- `homelab vpn status` - shows all peers
- `homelab vpn add-peer "danny-phone"` - generates config + QR
- `homelab vpn remove-peer "danny-phone"` - removes peer

**Branch:** `feature/vpn-management`

---

### Day 4: DNS & Monitoring Integration
**Goal:** AdGuard stats and Prometheus/Grafana integration

**Tasks:**

**DNS (AdGuard):**
- [ ] Create IAdGuardClient interface
- [ ] Implement AdGuardClient (AdGuard Home API)
- [ ] Implement MockAdGuardClient
- [ ] Create DnsCommand
- [ ] Implement `homelab dns stats` (queries, blocks, %)
- [ ] Implement `homelab dns blocked` (top blocked domains)
- [ ] Implement `homelab dns update-filters`

**Monitoring (Prometheus/Grafana):**
- [ ] Create IPrometheusClient + IGrafanaClient
- [ ] Implement real + mock clients
- [ ] Create MonitorCommand
- [ ] Implement `homelab monitor alerts` (active Prometheus alerts)
- [ ] Implement `homelab monitor targets` (scrape targets status)
- [ ] Implement `homelab monitor dashboard` (open Grafana in browser)

**Deliverables:**
- Full DNS statistics from CLI
- Monitoring integration working
- Quick access to dashboards

**Branch:** `feature/dns-monitoring`

---

### Day 5: Enhanced Status & Dependencies
**Goal:** Service dependencies and beautiful enhanced status

**Tasks:**
- [ ] Create ServiceDependencyGraph
- [ ] Define dependencies (VPN → DNS, Monitor → All)
- [ ] Implement smart startup order
- [ ] Enhance `homelab status` with:
  - Service-specific metrics (DNS queries/sec, VPN peers, alerts)
  - Dependency visualization
  - Interactive menu (press 'r' to restart failing service)
- [ ] Add `--watch` flag for live updates

**Deliverables:**
- Beautiful enhanced status dashboard
- Service dependency awareness
- Interactive status command

**Branch:** `feature/enhanced-status`

---

### Day 6: Remote Management (SSH)
**Goal:** Manage Mac Mini from MacBook

**Tasks:**
- [ ] Create ISshService interface
- [ ] Implement SshDockerService (SSH.NET)
- [ ] Create RemoteCommand
- [ ] Implement `homelab remote connect <ip>` (save connection)
- [ ] Implement `homelab remote status` (status on Mac Mini)
- [ ] Implement `homelab remote sync` (push/pull compose files)
- [ ] Store connection profiles (~/.homelab/remotes.yaml)
- [ ] Add `--remote` flag to all commands

**Deliverables:**
- Can execute commands on Mac Mini via SSH
- Configuration sync working
- All commands work remotely

**Branch:** `feature/remote-management`

---

### Day 7: Polish, Testing & Release
**Goal:** v1.5.0 release ready

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

**Branch:** `release/v1.5.0`

---

## 🛠️ Development Environment Setup

### Mock HomeLab Docker Compose

Create `~/Projects/homelab-mock/docker-compose.yml`:

```yaml
version: '3.8'

networks:
  homelab:
    driver: bridge

services:
  # DNS & Ad Blocking
  adguard:
    image: adguard/adguardhome
    container_name: homelab_adguard
    ports:
      - "3000:3000"   # Web UI
      - "53:53/tcp"   # DNS
      - "53:53/udp"
    volumes:
      - ./data/adguard/work:/opt/adguardhome/work
      - ./data/adguard/conf:/opt/adguardhome/conf
    networks:
      - homelab

  # VPN Server
  wireguard:
    image: linuxserver/wireguard
    container_name: homelab_wireguard
    cap_add:
      - NET_ADMIN
      - SYS_MODULE
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Europe/Prague
      - SERVERURL=auto
      - PEERS=3
    ports:
      - "51820:51820/udp"
    volumes:
      - ./data/wireguard:/config
      - /lib/modules:/lib/modules:ro
    sysctls:
      - net.ipv4.conf.all.src_valid_mark=1
    networks:
      - homelab

  # Metrics Collection
  prometheus:
    image: prom/prometheus:latest
    container_name: homelab_prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./data/prometheus:/prometheus
      - ./config/prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
    networks:
      - homelab

  # Dashboards
  grafana:
    image: grafana/grafana:latest
    container_name: homelab_grafana
    ports:
      - "3001:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - ./data/grafana:/var/lib/grafana
    networks:
      - homelab
    depends_on:
      - prometheus

  # System Metrics
  node-exporter:
    image: prom/node-exporter:latest
    container_name: homelab_node_exporter
    ports:
      - "9100:9100"
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro
    networks:
      - homelab
```

### Configuration File

Create `config/homelab-cli.yaml`:

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
    host: "192.168.1.100"  # Your Mac Mini IP
    user: "your-username"
    docker_host: "unix:///var/run/docker.sock"
```

### NuGet Packages to Install

```bash
dotnet add package System.Net.Http.Json      # HTTP API calls
dotnet add package YamlDotNet                # (already installed)
dotnet add package SSH.NET                   # SSH for remote management
dotnet add package QRCoder                   # QR codes for VPN configs
dotnet add package Moq                       # Mocking framework for tests
dotnet add package xunit                     # Testing framework
dotnet add package FluentAssertions          # Better test assertions
```

---

## 🌿 Git Workflow (CRITICAL!)

### Feature Branch Strategy

```bash
# ❌ NEVER do this (what you did in Phase 1-4)
git commit -m "add feature" && git push origin main

# ✅ ALWAYS do this (Phase 5 onwards)
git checkout -b feature/service-discovery
# ... work on feature ...
git commit -m "feat: implement service discovery"
git push origin feature/service-discovery
# Create PR on GitHub
# Run /code-review
# Merge PR
# Delete branch
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

## 🎯 Milestones & Releases

### v1.1.0 - Service Discovery & Health (Day 2)
- ✅ Service discovery working
- ✅ Health checks for all services
- ✅ Enhanced status dashboard

### v1.2.0 - VPN Management (Day 3)
- ✅ WireGuard peer management
- ✅ QR code generation
- ✅ Full VPN control from CLI

### v1.3.0 - DNS & Monitoring (Day 4)
- ✅ AdGuard statistics
- ✅ Prometheus/Grafana integration
- ✅ Monitoring dashboards

### v1.4.0 - Enhanced Features (Day 5)
- ✅ Service dependencies
- ✅ Beautiful status dashboard
- ✅ Interactive menus

### v1.5.0 - Remote Management (Day 6-7)
- ✅ SSH to Mac Mini
- ✅ Remote command execution
- ✅ Complete Phase 5

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

## 🚀 Immediate Next Steps (Awaiting Your Approval)

Once you approve this plan, here's what happens:

### Step 1: Environment Setup (15 minutes)
```bash
# Create mock homelab
mkdir -p ~/Projects/homelab-mock
cd ~/Projects/homelab-mock
# Create docker-compose.yml (from above)
docker-compose up -d

# Install NuGet packages
cd ~/Projects/homelab
dotnet add package System.Net.Http.Json
dotnet add package SSH.NET
dotnet add package QRCoder
dotnet add package Moq
dotnet add package xunit
dotnet add package FluentAssertions

# Create config directory
mkdir -p config
# Create config/homelab-cli.yaml (from above)
```

### Step 2: Start Day 1 Implementation
```bash
# Create feature branch
git checkout -b feature/phase5-foundation

# Start coding!
```

---

## ❓ Questions for You to Validate

**Before we start, please confirm:**

1. ✅ **Timeline:** 5-7 days is aggressive but achievable with Claude Code?
2. ✅ **Environment:** Mock homelab on MacBook is acceptable for development?
3. ✅ **Scope:** All Day 1-7 features are what you want?
4. ✅ **Workflow:** Feature branches + PRs + code review for everything?
5. ✅ **Priorities:** Are there features you want to skip or add?

**Any changes to make before we start?**

---

## 🎯 Ready to Start?

**Say "APPROVED" and I'll:**
1. Create the environment setup scripts
2. Create the first feature branch
3. Start implementing Day 1: Foundation

**Or tell me what to adjust first!** 🚀
