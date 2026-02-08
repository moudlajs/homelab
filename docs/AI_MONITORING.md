# AI Monitoring Guide

## What It Does

The AI monitoring collects real-time data from your homelab and sends it to Claude Haiku for analysis. Two commands:

- **`homelab monitor report`** — Full health summary with recommendations
- **`homelab monitor ask "question"`** — Ask anything about your homelab state

## Where Data Comes From

The `SystemDataCollector` gathers data from **3 sources** in parallel:

### 1. System Metrics (always available)

Collected via macOS shell commands. No external services needed.

| Metric | How it's collected | Command |
|--------|-------------------|---------|
| CPU count | Hardware info | `sysctl -n hw.ncpu` |
| CPU usage % | Sample 1 second | `top -l 1 -s 0 -n 0` (parse idle%) |
| Total RAM | Hardware info | `sysctl -n hw.memsize` |
| Used RAM | Page statistics | `vm_stat` (active + wired + compressed pages) |
| Disk usage | Filesystem stats | `df -h /` |
| Uptime | System uptime | `uptime` |

### 2. Docker Metrics (when Docker/OrbStack is running)

Collected via Docker API using the existing `IDockerService`.

| Metric | Source |
|--------|--------|
| Container list | `DockerService.ListContainersAsync()` |
| Running/stopped count | Container state |
| Container names | Container metadata |

**If Docker is not available**: Skipped gracefully, noted in report.

### 3. Prometheus Metrics (when Prometheus is running)

Collected via HTTP API using the existing `IPrometheusClient`.

| Metric | Source |
|--------|--------|
| Active alerts | `GET /api/v1/alerts` |
| Alert severity/name | Alert labels |
| Scrape targets | `GET /api/v1/targets` |
| Targets up/down | Target health status |

**If Prometheus is not available**: Skipped gracefully, noted in report.

## How the AI Works

```
User runs command
       │
       ▼
┌──────────────┐
│  Collect     │  System + Docker + Prometheus
│  Data        │  (parallel, 10s timeout each)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Format      │  Structured text prompt
│  as Prompt   │  (~200 tokens)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Send to     │  POST api.anthropic.com/v1/messages
│  Claude      │  (30s timeout)
│  Haiku       │  Single request, no streaming
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Display     │  Render in terminal + show cost
│  Response    │
└──────────────┘
```

## Safety Guarantees

### Cost control
- **Single API call per command** — no loops, no retries, no background requests
- **Max tokens capped** — 1024 for report, 512 for ask (prevents runaway output)
- **Cost displayed** — every response shows exact token usage and cost
- **On-demand only** — no cron, no scheduler, no background polling
- Typical cost: **$0.001-0.002 per query**

### Timeouts
- **API timeout: 30 seconds** — request is cancelled if no response
- **Shell command timeout: 10 seconds** — prevents hung processes
- **No retries** — if something fails, it fails once and shows the error

### Resource cleanup
- `HttpRequestMessage` disposed after each request (`using`)
- `Process` objects disposed after shell commands (`using`)
- `CancellationToken` propagated for clean cancellation
- Shared `HttpClient` (singleton) — no connection leaks

### Failure modes
| Scenario | What happens |
|----------|-------------|
| No API key | Shows raw data, tells you to configure |
| Invalid API key | Shows error message + raw data fallback |
| API timeout | "Request timed out after 30 seconds" |
| No credits | Shows error from API + raw data fallback |
| Docker down | Data collected without Docker, noted in report |
| Prometheus down | Data collected without Prometheus, noted in report |
| Shell command hangs | Killed after 10 seconds, partial data used |
| Network offline | API call fails fast, raw data shown |

## Configuration

File: `~/.config/homelab/homelab-cli.yaml`

```yaml
services:
  ai:
    provider: "anthropic"
    model: "claude-haiku-4-5-20251001"
    token: "sk-ant-api03-..."
    enabled: true
```

| Field | Description |
|-------|-------------|
| `provider` | LLM provider (only "anthropic" supported now) |
| `model` | Model to use (default: `claude-haiku-4-5-20251001`) |
| `token` | API key from https://console.anthropic.com/settings/keys |
| `enabled` | Toggle AI on/off |

## Usage Examples

```bash
# Full AI health report
homelab monitor report

# Raw data only (no AI, no API call)
homelab monitor report --raw

# Ask a question
homelab monitor ask "is my disk space running low?"
homelab monitor ask "what containers are stopped and why?"
homelab monitor ask "should I be worried about anything?"

# Works in interactive shell too
homelab
> monitor report
> monitor ask "how's memory usage?"
```

## Architecture

```
Commands/Monitor/
├── MonitorReportCommand.cs   # homelab monitor report
├── MonitorAskCommand.cs      # homelab monitor ask
├── MonitorAlertsCommand.cs   # homelab monitor alerts (existing)
├── MonitorTargetsCommand.cs  # homelab monitor targets (existing)
└── MonitorDashboardCommand.cs # homelab monitor dashboard (existing)

Services/AI/
├── AnthropicLlmService.cs    # Claude API client (raw HTTP)
└── SystemDataCollector.cs    # Collects system/Docker/Prometheus data

Services/Abstractions/
├── ILlmService.cs            # LLM provider interface
└── ISystemDataCollector.cs   # Data collector interface

Models/AI/
├── LlmResponse.cs            # API response model
└── HomelabDataSnapshot.cs    # Collected data models
```

## Cost Estimation

| Usage | Tokens/query | Cost/query | Monthly (5x/day) |
|-------|-------------|-----------|-------------------|
| Report | ~500 | ~$0.002 | ~$0.30 |
| Ask | ~350 | ~$0.001 | ~$0.15 |
| **Total** | | | **~$0.50/month** |
