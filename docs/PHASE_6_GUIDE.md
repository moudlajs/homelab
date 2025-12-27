# Phase 6: Monitoring & Observability - Complete Guide

**Version:** v1.3.0
**Released:** December 27, 2025
**Status:** ✅ Complete

---

## 🎯 Overview

Phase 6 adds comprehensive monitoring and observability features to the HomeLab CLI. This release focuses on three major areas:

1. **Export Commands** - Multi-format output for automation
2. **Uptime Kuma Integration** - Service uptime monitoring
3. **Speedtest Tracker Integration** - Internet speed tracking

---

## 📊 Feature 1: Export Commands

### What It Does

Export any command output in multiple formats for automation, analysis, and integration with other tools.

### Supported Formats

| Format | Use Case | Example |
|--------|----------|---------|
| **JSON** | Automation, APIs, scripting | `homelab status -o json` |
| **CSV** | Spreadsheets, data analysis | `homelab status -o csv` |
| **YAML** | Configuration files | `homelab status -o yaml` |
| **Table** | Human-readable (default) | `homelab status` |

### Basic Usage

```bash
# Print JSON to stdout
homelab status --output json

# Save to file
homelab status --output csv --export services.csv

# Short flag
homelab status -o json
```

### Advanced Examples

#### Filter Unhealthy Services with jq
```bash
homelab status -o json | jq '.[] | select(.IsHealthy == false)'
```

#### Export for Monitoring System
```bash
# Save hourly status snapshots
homelab status -o json --export /var/log/homelab/status-$(date +%Y%m%d-%H).json
```

#### Import to Excel
```bash
# Export as CSV and open in Excel
homelab status -o csv --export ~/Desktop/homelab-status.csv
open ~/Desktop/homelab-status.csv
```

#### Generate Reports
```bash
# Create daily report
cat << EOF > report.sh
#!/bin/bash
DATE=$(date +%Y-%m-%d)
homelab status -o json --export reports/status-\$DATE.json
homelab uptime alerts -o csv --export reports/alerts-\$DATE.csv
homelab speedtest stats -o yaml --export reports/speed-\$DATE.yaml
EOF
```

### Output Examples

#### JSON Format
```json
[
  {
    "service": "adguard",
    "type": "Dns",
    "isRunning": true,
    "isHealthy": true,
    "status": "Healthy",
    "message": null,
    "metrics": {
      "Total Queries": "12,450",
      "Blocked": "3,120"
    }
  }
]
```

#### CSV Format
```csv
Service,Type,IsRunning,IsHealthy,Status,Message
adguard,Dns,True,True,Healthy,
wireguard,Vpn,True,True,Healthy,
prometheus,Metrics,False,False,Unhealthy,Container not running
```

#### YAML Format
```yaml
- service: adguard
  type: Dns
  isRunning: true
  isHealthy: true
  status: Healthy
  metrics:
    Total Queries: "12,450"
    Blocked: "3,120"
```

### Commands with Export Support

Currently supported:
- ✅ `homelab status` - Full support

Coming soon:
- 🔜 `homelab vpn status`
- 🔜 `homelab dns stats`
- 🔜 `homelab monitor alerts`
- 🔜 `homelab uptime status`
- 🔜 `homelab speedtest stats`

---

## ⏱️ Feature 2: Uptime Kuma Integration

### What It Does

Track service uptime, monitor availability, and get alerts when services go down. Integrates with [Uptime Kuma](https://github.com/louislam/uptime-kuma) for comprehensive uptime monitoring.

### Prerequisites

**Option 1: Run Uptime Kuma (Recommended)**
```bash
docker run -d \
  --name uptime-kuma \
  -p 3001:3001 \
  -v uptime-kuma:/app/data \
  louislam/uptime-kuma:1
```

**Option 2: Use Mock Data**
If Uptime Kuma isn't running, the CLI will automatically show mock data for demonstration.

### Commands

#### View Monitor Status
```bash
homelab uptime status
homelab uptime st        # Short alias
homelab uptime ls        # Alternative alias
```

**Example Output:**
```
┌──────────────┬────────────────────────┬──────┬────────┬──────────┬──────────┐
│ Monitor      │ URL                    │ Type │ Status │ Uptime   │ Response │
├──────────────┼────────────────────────┼──────┼────────┼──────────┼──────────┤
│ AdGuard Home │ http://localhost:3000  │ http │ 🟢 UP   │ 99.98%   │ 45ms     │
│ WireGuard    │ http://localhost:51820 │ port │ 🟢 UP   │ 100.00%  │ 12ms     │
│ Prometheus   │ http://localhost:9090  │ http │ 🔴 DOWN │ 85.50%   │ -        │
└──────────────┴────────────────────────┴──────┴────────┴──────────┴──────────┘

Summary: ✓ Up: 2/3    ✗ Down: 1/3    ⚡ Avg Uptime: 95.16%
```

#### View Recent Alerts
```bash
homelab uptime alerts
homelab uptime al        # Short alias

# Limit number of alerts shown
homelab uptime alerts --limit 5
```

**Example Output:**
```
┌──────────────┬───────────┬──────────────┬──────────┬─────────────────────┐
│ Time         │ Monitor   │ Status       │ Duration │ Message             │
├──────────────┼───────────┼──────────────┼──────────┼─────────────────────┤
│ 12-27 20:00  │ Prometheus│ 🔴 DOWN      │ 2h       │ Connection refused  │
│ 12-26 10:15  │ AdGuard   │ 🟢 RECOVERED │ 5m       │ HTTP 200 OK         │
└──────────────┴───────────┴──────────────┴──────────┴─────────────────────┘

Summary: ⚠ Active: 1    ✓ Resolved: 1    ⏱ Total Downtime: 2h 5m
```

#### Add New Monitor
```bash
homelab uptime add <name> <url> [--type TYPE]

# Examples
homelab uptime add adguard http://localhost:3000
homelab uptime add wireguard http://localhost:51820 --type port
homelab uptime add grafana http://localhost:3001 --type http
```

**Monitor Types:**
- `http` - HTTP/HTTPS endpoint (default)
- `port` - TCP port check
- `ping` - ICMP ping

### Use Cases

#### Daily Health Check
```bash
# Quick morning check
homelab uptime st

# Any incidents overnight?
homelab uptime alerts --limit 10
```

#### SLA Tracking
```bash
# Export uptime data monthly
homelab uptime status -o csv --export uptime-$(date +%Y-%m).csv

# Track if meeting 99.9% SLA
homelab uptime status -o json | jq '.[] | select(.UptimePercentage < 99.9)'
```

#### Incident Response
```bash
# Check what's down
homelab uptime status | grep DOWN

# Get incident details
homelab uptime alerts --limit 1
```

---

## 🚀 Feature 3: Speedtest Tracker Integration

### What It Does

Monitor internet connection speed over time, track performance trends, and identify network issues. Integrates with [Speedtest Tracker](https://github.com/alexjustesen/speedtest-tracker).

### Prerequisites

**Option 1: Run Speedtest Tracker (Recommended)**
```bash
docker run -d \
  --name speedtest-tracker \
  -p 8080:80 \
  -e PUID=1000 \
  -e PGID=1000 \
  -v speedtest-data:/config \
  linuxserver/speedtest-tracker:latest
```

**Option 2: Use Mock Data**
If Speedtest Tracker isn't running, the CLI will automatically show mock data.

### Commands

#### Run Speed Test
```bash
homelab speedtest run
```

**What It Does:**
- Triggers a new speed test via Speedtest Tracker
- Takes 30-60 seconds to complete
- Tests download, upload, and ping

**Example Output:**
```
Running speed test...
✓ Speed test completed successfully!
Use 'homelab speedtest stats' to view results
```

#### View Statistics
```bash
homelab speedtest stats
homelab speedtest st        # Short alias
```

**Example Output:**
```
30-Day Statistics:
┌──────────────────────┬────────────────┐
│ Average Download:    │ 455.5 Mbps     │
│ Average Upload:      │ 47.2 Mbps      │
│ Average Ping:        │ 18.5 ms        │
│ Speed Range:         │ 380.5-510.2 Mbps│
│ Total Tests (30d):   │ 240            │
└──────────────────────┴────────────────┘

Recent Test Results:
┌──────────────┬──────────────┬─────────────┬────────┬───────────┐
│ Time         │ Download     │ Upload      │ Ping   │ Server    │
├──────────────┼──────────────┼─────────────┼────────┼───────────┤
│ 12-27 22:00  │ 465.2 Mbps   │ 48.5 Mbps   │ 15 ms  │ Cloudflare│
│ 12-27 16:00  │ 448.7 Mbps   │ 46.2 Mbps   │ 18 ms  │ Google    │
│ 12-27 10:00  │ 441.3 Mbps   │ 47.8 Mbps   │ 20 ms  │ Cloudflare│
└──────────────┴──────────────┴─────────────┴────────┴───────────┘

Speed Comparison (Bar Chart):
Avg Download  ████████████████████████████████ 455 Mbps
Avg Upload    ████ 47 Mbps
Min Download  ██████████████████████████ 380 Mbps
Max Download  ████████████████████████████████████ 510 Mbps
```

### Use Cases

#### ISP Performance Monitoring
```bash
# Run test every 6 hours via cron
0 */6 * * * /usr/local/bin/homelab speedtest run

# Check if meeting advertised speeds
homelab speedtest stats
```

#### Troubleshooting Slow Internet
```bash
# Run immediate test
homelab speedtest run

# Check recent trends
homelab speedtest stats

# Export for analysis
homelab speedtest stats -o csv --export speeds.csv
```

#### Track ISP Outages
```bash
# Export historical data
homelab speedtest stats -o json | jq '.recentResults[] | select(.DownloadSpeed < 100)'
```

---

## 🛠️ Configuration

### Service URLs

Default URLs (used when services aren't configured):
- **Uptime Kuma**: `http://localhost:3001`
- **Speedtest Tracker**: `http://localhost:8080`

To change these, update your config file (future enhancement):
```yaml
# config/homelab-cli.yaml
services:
  uptime-kuma:
    url: "http://your-server:3001"
  speedtest-tracker:
    url: "http://your-server:8080"
```

---

## 📋 Complete Command Reference

### Export Flags (Available on supported commands)

| Flag | Description | Example |
|------|-------------|---------|
| `-o, --output <FORMAT>` | Output format (json, csv, yaml, table) | `--output json` |
| `--export <FILE>` | Save to file instead of stdout | `--export data.csv` |

### Uptime Commands

| Command | Aliases | Description |
|---------|---------|-------------|
| `homelab uptime status` | `st`, `ls` | Show all monitored services |
| `homelab uptime alerts` | `al` | Show recent incidents |
| `homelab uptime add <name> <url>` | - | Add new monitor |

**Options:**
- `--limit <COUNT>` - Limit number of alerts shown (default: 10)
- `--type <TYPE>` - Monitor type: http, port, ping (default: http)

### Speedtest Commands

| Command | Aliases | Description |
|---------|---------|-------------|
| `homelab speedtest run` | - | Run new speed test |
| `homelab speedtest stats` | `st` | Show statistics and history |

---

## 🎯 Real-World Workflows

### Morning Health Check Routine
```bash
#!/bin/bash
# morning-check.sh

echo "=== HomeLab Morning Health Check ==="
echo ""

echo "📊 Service Status:"
homelab status
echo ""

echo "⏱️ Uptime Summary:"
homelab uptime status
echo ""

echo "🚀 Internet Speed:"
homelab speedtest stats
echo ""

echo "⚠️ Recent Incidents:"
homelab uptime alerts --limit 5
```

### Weekly Report Generation
```bash
#!/bin/bash
# weekly-report.sh

WEEK=$(date +%Y-W%U)
REPORT_DIR="reports/$WEEK"
mkdir -p "$REPORT_DIR"

# Export all data
homelab status -o csv --export "$REPORT_DIR/services.csv"
homelab uptime status -o json --export "$REPORT_DIR/uptime.json"
homelab uptime alerts -o csv --export "$REPORT_DIR/incidents.csv"
homelab speedtest stats -o yaml --export "$REPORT_DIR/speed.yaml"

echo "✅ Weekly report generated in $REPORT_DIR"
```

### Automated Monitoring with Cron
```bash
# Add to crontab: crontab -e

# Run speed test every 6 hours
0 */6 * * * /usr/local/bin/homelab speedtest run

# Export status snapshot hourly
0 * * * * /usr/local/bin/homelab status -o json --export /var/log/homelab/status-$(date +\%H).json

# Daily uptime report at midnight
0 0 * * * /usr/local/bin/homelab uptime alerts -o csv --export ~/reports/incidents-$(date +\%Y-\%m-\%d).csv
```

---

## 🐛 Troubleshooting

### Uptime Kuma Not Connecting

**Symptom:** `Uptime Kuma is not healthy: Connection refused`

**Solutions:**
1. Check if Uptime Kuma is running:
   ```bash
   docker ps | grep uptime-kuma
   ```

2. Verify port 3001 is accessible:
   ```bash
   curl http://localhost:3001
   ```

3. Use mock data for testing:
   - CLI automatically falls back to mock data
   - No configuration needed

### Speedtest Tracker Not Responding

**Symptom:** `Speedtest Tracker is not healthy: Failed to connect`

**Solutions:**
1. Check if container is running:
   ```bash
   docker ps | grep speedtest-tracker
   ```

2. Check logs:
   ```bash
   docker logs speedtest-tracker
   ```

3. Mock data is shown automatically

### Export to CSV Shows Garbled Text

**Issue:** Special characters not displaying correctly

**Solution:**
```bash
# Specify UTF-8 encoding when opening
homelab status -o csv --export data.csv
iconv -f UTF-8 -t UTF-8 data.csv > data-utf8.csv
```

---

## 🚀 Next Steps

### Extend Export Support

Add export flags to more commands:
```bash
homelab vpn status -o json
homelab dns stats -o csv --export dns.csv
homelab monitor alerts -o yaml
```

### Custom Dashboards

Build custom monitoring dashboards using exported data:
```bash
# Combine all metrics
{
  echo "{"
  echo "  \"services\": $(homelab status -o json),"
  echo "  \"uptime\": $(homelab uptime status -o json),"
  echo "  \"speed\": $(homelab speedtest stats -o json)"
  echo "}"
} > dashboard.json
```

### Alerting Integration

Integrate with alerting systems:
```bash
# Check for unhealthy services
UNHEALTHY=$(homelab status -o json | jq '[.[] | select(.IsHealthy == false)] | length')

if [ $UNHEALTHY -gt 0 ]; then
  # Send alert via webhook, email, etc.
  curl -X POST https://your-webhook.com/alert \
    -d "message=Warning: $UNHEALTHY services are unhealthy"
fi
```

---

## 📚 Additional Resources

- [Uptime Kuma GitHub](https://github.com/louislam/uptime-kuma)
- [Speedtest Tracker GitHub](https://github.com/alexjustesen/speedtest-tracker)
- [HomeLab CLI Documentation](../README.md)
- [Phase 5 Guide](./PHASE_5_COMPLETE.md)

---

**Version:** v1.3.0
**Last Updated:** December 27, 2025
**Status:** ✅ Production Ready
