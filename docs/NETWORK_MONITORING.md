# Network Security & Monitoring Guide

> Comprehensive network monitoring, traffic analysis, and intrusion detection for your homelab.

## Overview

The HomeLab CLI includes a complete network security and monitoring solution that provides:

- **Device Discovery**: Scan your network to find connected devices and open ports
- **Traffic Analysis**: Monitor bandwidth usage, track top talkers, and analyze protocols
- **Intrusion Detection**: Real-time security alerts for suspicious activity

### Architecture

The network monitoring module uses a hybrid architecture combining three specialized tools:

| Tool | Purpose | Integration | Resource Usage |
|------|---------|-------------|----------------|
| **nmap** | Device/port scanning | Direct CLI execution | Low (on-demand) |
| **ntopng** | Traffic monitoring | HTTP REST API (Docker) | Medium (~200MB) |
| **Suricata** | Intrusion detection | Log parsing (Docker) | Medium (~300MB) |

---

## Installation

### 1. Install nmap

nmap is required for network and port scanning:

```bash
# macOS
brew install nmap

# Verify installation
nmap --version
```

### 2. Start Docker Services

Start ntopng and Suricata containers:

```bash
# From homelab directory
docker-compose up -d ntopng suricata

# Verify containers are running
docker ps | grep -E "ntopng|suricata"
```

### 3. Configure Services

Edit `config/homelab-cli.yaml`:

```yaml
services:
  ntopng:
    url: "http://localhost:3002"
    enabled: true

  suricata:
    log_path: "~/Repos/homelab/data/suricata/logs/eve.json"
    enabled: true

network:
  default_range: "192.168.1.0/24"  # Your network range
  scan_options:
    quick_scan: true
  monitoring:
    alert_severity: "medium"  # critical, high, medium, low
```

### 4. Suricata Configuration (Optional)

Create `config/suricata/suricata.yaml` to customize Suricata behavior. The default configuration is sufficient for most use cases.

---

## Commands

### Network Scan

Discover all devices on your network:

```bash
# Scan default network range
homelab network scan

# Scan specific range
homelab network scan --range 192.168.1.0/24

# Quick scan (no port detection, faster)
homelab network scan --quick

# Export to JSON
homelab network scan --output json --export devices.json
```

**Output:**
- IP address, MAC address, hostname
- Vendor identification
- Device status (up/down)
- Open ports (if not quick scan)
- OS guess

### Port Scan

Scan open ports on specific devices:

```bash
# Scan common ports on a device
homelab network ports --device 192.168.1.10

# Scan all ports (slower)
homelab network ports --device 192.168.1.10 --all

# Export results
homelab network ports --device 192.168.1.10 --output csv --export ports.csv
```

**Output:**
- Port number
- Protocol (TCP/UDP)
- Service name
- Service version
- State (open/closed/filtered)

### List Tracked Devices

Show all devices tracked by ntopng:

```bash
# List all devices
homelab network devices

# Show only active devices
homelab network devices --active

# Export to YAML
homelab network devices --output yaml --export devices.yaml
```

**Output:**
- Device name, IP, MAC address
- First seen / last seen timestamps
- Bytes sent / received
- Throughput (if active)
- Operating system
- Active status

### Traffic Statistics

Display network traffic statistics:

```bash
# Show overall traffic stats
homelab network traffic

# Filter by device
homelab network traffic --device 192.168.1.10

# Show top 20 talkers
homelab network traffic --top 20

# Export statistics
homelab network traffic --output json --export traffic.json
```

**Output:**
- Total bytes transferred
- Active flows count
- Top talkers (ranked with colors: gold, silver, bronze)
- Protocol distribution (bar chart)
- Traffic overview panel

### Security Alerts

Display security alerts from Suricata IDS:

```bash
# Show all recent alerts
homelab network intrusion

# Filter by severity
homelab network intrusion --severity critical
homelab network intrusion --severity high

# Limit results
homelab network intrusion --limit 100

# Export alerts
homelab network intrusion --output json --export alerts.json
```

**Aliases**: `homelab network alerts`

**Output:**
- Time ago
- Severity (color-coded: red=critical, orange=high, yellow=medium, dim=low)
- Alert type
- Source IP:port
- Destination IP:port
- Protocol
- Category
- Alert summary counts

### Network Status Dashboard

Comprehensive network health overview:

```bash
# Show complete network status
homelab network status
```

**Alias**: `homelab network st`

**Output:**
- Service health (nmap, ntopng, Suricata)
- Active devices count
- Total network traffic
- Top 5 bandwidth consumers
- Security alerts (last 24 hours)
- Latest critical alert
- Overall status (healthy, warning, critical)

---

## Configuration

### Network Range

Set your default network range in `config/homelab-cli.yaml`:

```yaml
network:
  default_range: "192.168.1.0/24"  # Adjust to your network
```

Common network ranges:
- Home network: `192.168.1.0/24` (192.168.1.1 - 192.168.1.254)
- Large home: `192.168.0.0/16` (192.168.0.0 - 192.168.255.255)
- Enterprise: `10.0.0.0/8` (10.0.0.0 - 10.255.255.255)

### Alert Severity Threshold

Configure minimum alert severity to reduce noise:

```yaml
network:
  monitoring:
    alert_severity: "medium"  # Only show medium, high, and critical
```

Options: `low`, `medium`, `high`, `critical`

### Docker Configuration

Customize ntopng and Suricata in `docker-compose.yml`:

```yaml
ntopng:
  image: ntop/ntopng:latest
  network_mode: host  # Required for traffic monitoring
  command: --community --http-port 3002

suricata:
  image: jasonish/suricata:latest
  network_mode: host  # Required for packet capture
  command: -i eth0  # eth0 for OrbStack/Docker VM, or your host interface
```

**Finding your network interface:**
```bash
# macOS/Linux
ifconfig
ip link show

# Look for your primary ethernet or wifi interface
# Usually: en0, eth0, wlan0
```

---

## Performance Tuning

### Scan Speed

```bash
# Quick scan (skip port detection)
homelab network scan --quick

# Specific range (smaller = faster)
homelab network scan --range 192.168.1.0/28  # Only 14 hosts
```

### Resource Limits

Docker Compose resource limits (add to services):

```yaml
ntopng:
  deploy:
    resources:
      limits:
        memory: 256M
        cpus: '0.5'

suricata:
  deploy:
    resources:
      limits:
        memory: 512M
        cpus: '1.0'
```

### Log Rotation

Suricata logs can grow large. Configure rotation:

```bash
# Create logrotate config
cat > /etc/logrotate.d/suricata <<EOF
/path/to/homelab/data/suricata/logs/*.log {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
}
EOF
```

---

## Troubleshooting

### "nmap not installed"

**Problem**: Network scan fails with "nmap is not installed"

**Solution**:
```bash
# macOS
brew install nmap

# Linux (Debian/Ubuntu)
sudo apt-get install nmap

# Verify
nmap --version
```

### "ntopng is not available"

**Problem**: `homelab network devices` shows service unavailable

**Solutions**:
1. Check if container is running:
   ```bash
   docker ps | grep ntopng
   ```

2. Start the container:
   ```bash
   docker-compose up -d ntopng
   ```

3. Check logs:
   ```bash
   docker logs homelab_ntopng
   ```

4. Verify configuration:
   ```bash
   # Should return HTTP 200
   curl http://localhost:3002
   ```

### "Suricata log file not found"

**Problem**: No security alerts displayed

**Solutions**:
1. Check if Suricata is running:
   ```bash
   docker ps | grep suricata
   ```

2. Verify log path in config matches actual path:
   ```yaml
   services:
     suricata:
       log_path: "~/Repos/homelab/data/suricata/logs/eve.json"
   ```

3. Check log file exists:
   ```bash
   ls -lh ~/Repos/homelab/data/suricata/logs/eve.json
   ```

4. Verify container has write permissions:
   ```bash
   docker logs homelab_suricata
   ```

### Port Scan Very Slow

**Problem**: `homelab network ports` takes minutes to complete

**Solutions**:
1. Use `--common` flag to only scan common ports
2. Scan specific range instead of all ports
3. Check if firewall is blocking/rate-limiting

### High Memory Usage

**Problem**: Docker containers using too much RAM

**Solutions**:
1. Add resource limits to docker-compose.yml (see Performance Tuning)
2. Reduce Suricata rule set (if using custom rules)
3. Clear ntopng data periodically:
   ```bash
   rm -rf data/ntopng/*
   docker-compose restart ntopng
   ```

### Permission Denied

**Problem**: Cannot capture network traffic

**Solutions**:
1. Ensure Docker has required capabilities:
   ```yaml
   cap_add:
     - NET_ADMIN  # Required for packet capture
   ```

2. Use host network mode:
   ```yaml
   network_mode: host
   ```

3. On Linux, may need to run Docker as root or configure capabilities

### No Devices Found

**Problem**: Network scan finds 0 devices

**Solutions**:
1. Verify network range is correct:
   ```bash
   # Check your IP
   ifconfig | grep inet

   # Scan that range
   homelab network scan --range YOUR_SUBNET/24
   ```

2. Check if ARP table is empty:
   ```bash
   arp -a
   ```

3. Firewall may be blocking ICMP/ARP:
   - Temporarily disable firewall to test
   - Configure firewall to allow local network scans

---

## Security Considerations

### Privacy & Ethics

- **Only scan networks you own or have permission to scan**
- Port scanning can be considered hostile by network administrators
- Always comply with your organization's security policy
- Disable monitoring in guest networks

### Alert Fatigue

Suricata may generate many alerts initially:

1. **Tune severity threshold**:
   ```yaml
   monitoring:
     alert_severity: "high"  # Reduce noise
   ```

2. **Filter known false positives** (customize Suricata rules)

3. **Regular review**: Check alerts weekly, investigate critical ones immediately

4. **Whitelist trusted IPs** in Suricata config

### Data Retention

Network monitoring data can contain sensitive information:

1. **Limit retention period**:
   - Rotate logs daily/weekly
   - Delete old ntopng data
   - Don't export sensitive data to insecure locations

2. **Secure export files**:
   ```bash
   # Encrypt exports
   homelab network devices --output json | gpg -e -r your@email.com > devices.gpg
   ```

3. **Access control**:
   - Restrict config file permissions
   - Don't commit sensitive configs to git
   - Use environment variables for credentials

### Network Load

- **Quick scans** for regular monitoring
- **Full scans** only when needed (e.g., weekly)
- **Rate limiting**: nmap has built-in rate limiting
- **Schedule scans** during off-peak hours

---

## Integration Examples

### Daily Security Report

```bash
#!/bin/bash
# Save as: ~/bin/network-security-report.sh

DATE=$(date +%Y-%m-%d)
REPORT_DIR=~/reports

mkdir -p $REPORT_DIR

# Scan network
homelab network scan --output json --export $REPORT_DIR/scan-$DATE.json

# Get critical alerts
homelab network intrusion --severity critical --output json --export $REPORT_DIR/alerts-$DATE.json

# Send notification if critical alerts found
CRITICAL_COUNT=$(jq 'length' $REPORT_DIR/alerts-$DATE.json)
if [ "$CRITICAL_COUNT" -gt 0 ]; then
    echo "⚠️ $CRITICAL_COUNT critical security alerts detected!" | mail -s "HomeLab Security Alert" your@email.com
fi
```

### Grafana Dashboard

Export network stats to Prometheus/Grafana:

```bash
# Export traffic stats hourly
*/60 * * * * homelab network traffic --output json > /var/lib/prometheus/network-traffic.json
```

### Slack Notifications

```bash
#!/bin/bash
# Critical alert notification

WEBHOOK_URL="https://hooks.slack.com/services/YOUR/WEBHOOK/URL"

homelab network intrusion --severity critical --output json | \
jq -r '.[] | "⚠️ *Critical Alert*\n*Type:* \(.AlertType)\n*From:* \(.SourceIp):\(.SourcePort)\n*Time:* \(.Timestamp)"' | \
while read -r alert; do
    curl -X POST -H 'Content-type: application/json' \
        --data "{\"text\":\"$alert\"}" \
        "$WEBHOOK_URL"
done
```

---

## Advanced Usage

### Continuous Monitoring

Run network status check every 5 minutes:

```bash
# Add to crontab: crontab -e
*/5 * * * * homelab network status --output json >> ~/logs/network-status.log
```

### Detect New Devices

Alert when new devices join the network:

```bash
#!/bin/bash
CURRENT=$(homelab network scan --output json)
PREVIOUS=$(cat ~/.homelab/last-scan.json 2>/dev/null || echo "[]")

NEW_DEVICES=$(jq -n --argjson current "$CURRENT" --argjson previous "$PREVIOUS" \
    '$current - $previous')

if [ "$(echo $NEW_DEVICES | jq 'length')" -gt 0 ]; then
    echo "New devices detected:"
    echo "$NEW_DEVICES" | jq -r '.[] | "  - \(.IpAddress) (\(.Hostname))"'

    # Send notification
    echo "$NEW_DEVICES" | mail -s "New Device Alert" your@email.com
fi

echo "$CURRENT" > ~/.homelab/last-scan.json
```

### Custom Alerts

Filter Suricata alerts by pattern:

```bash
# SSH brute force attempts
homelab network intrusion --output json | jq '.[] | select(.Signature | contains("SSH Brute"))'

# Malware communication
homelab network intrusion --output json | jq '.[] | select(.Category | contains("Malware"))'
```

---

## Command Reference

| Command | Purpose | Key Options |
|---------|---------|-------------|
| `network scan` | Discover devices | `--range`, `--quick`, `--output`, `--export` |
| `network ports` | Scan ports | `--device`, `--common`, `--output`, `--export` |
| `network devices` | List tracked devices | `--active`, `--output`, `--export` |
| `network traffic` | Traffic statistics | `--device`, `--top`, `--output`, `--export` |
| `network intrusion` | Security alerts | `--severity`, `--limit`, `--output`, `--export` |
| `network status` | Overall health | None |

**Output formats**: `table` (default), `json`, `csv`, `yaml`

---

## Roadmap

Future enhancements planned:

- [ ] Email notifications for critical alerts
- [ ] Bandwidth usage graphs (historical)
- [ ] Device name resolution (DNS reverse lookup)
- [ ] MAC vendor database updates
- [ ] Custom Suricata rules management
- [ ] Network topology visualization
- [ ] Automated remediation actions
- [ ] Mobile app integration
- [ ] Multi-site monitoring

---

## Resources

### Documentation
- [nmap Documentation](https://nmap.org/docs.html)
- [ntopng Documentation](https://www.ntop.org/guides/ntopng/)
- [Suricata Documentation](https://suricata.readthedocs.io/)

### Suricata Rules
- [Emerging Threats Open Rules](https://rules.emergingthreats.net/open/)
- [Suricata Rule Sets](https://github.com/OISF/suricata)

### Community
- [HomeLab GitHub Issues](https://github.com/moudlajs/homelab/issues)
- [HomeLab Discussions](https://github.com/moudlajs/homelab/discussions)

---

## Support

If you encounter issues:

1. Check [Troubleshooting](#troubleshooting) section above
2. Review Docker container logs: `docker logs homelab_ntopng` or `docker logs homelab_suricata`
3. [Open an issue](https://github.com/moudlajs/homelab/issues) with:
   - Command executed
   - Error message
   - `homelab --version`
   - Operating system
   - Docker version

---

**Generated with [Claude Code](https://claude.com/claude-code)**
