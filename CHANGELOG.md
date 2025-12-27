# Changelog

All notable changes to the HomeLab CLI project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] - 2025-01-XX

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

## [1.0.0] - 2024-XX-XX

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

[1.5.0]: https://github.com/moudlajs/homelab/compare/v1.0.0...v1.5.0
[1.0.0]: https://github.com/moudlajs/homelab/releases/tag/v1.0.0
