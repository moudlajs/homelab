# TV Control - LG WebOS Smart TV

Control your LG WebOS TV from the command line. Wake it up, launch apps, send remote keys, and more.

---

## Quick Start

```bash
# First time setup - pair with your TV
homelab tv setup

# Turn TV on
homelab tv on

# Turn TV on and launch an app
homelab tv on --app netflix

# Turn TV on, launch app, and start playing
homelab tv on --app default --key enter

# Turn TV off
homelab tv off
```

---

## Setup

### 1. Find Your TV's IP and MAC Address

- **IP Address**: Check your router's connected devices, or go to TV Settings > Network
- **MAC Address**: TV Settings > Network > Wi-Fi/Wired Connection > Advanced Settings

### 2. Run Setup Wizard

```bash
homelab tv setup --ip 192.168.1.102 --mac AA:BB:CC:DD:EE:FF --name "Living Room TV"
```

Or run interactively:
```bash
homelab tv setup
```

### 3. Accept Pairing on TV

When prompted, a pairing dialog will appear on your TV screen. Use your remote to accept it.

### 4. Set Default App (Optional)

Edit `~/.homelab/tv.json` and add your preferred app:
```json
{
  "DefaultApp": "netflix"
}
```

Use `homelab tv apps` to see available app IDs.

---

## Commands

### `tv status`
Check if TV is online and paired.

```bash
homelab tv status
```

### `tv on`
Turn TV on using Wake-on-LAN.

```bash
# Just turn on
homelab tv on

# Turn on and launch app
homelab tv on --app netflix
homelab tv on --app default    # uses DefaultApp from config

# Turn on, launch app, and send keys
homelab tv on --app default --key enter
homelab tv on --app youtube -k enter -k enter  # multiple keys
```

**Options:**
- `-a, --app <APP>` - Launch app after wake (use app ID, name, or "default")
- `-k, --key <KEY>` - Send remote key after app loads (can be used multiple times)
- `--delay <MS>` - Delay between key presses (default: 500ms)

### `tv off`
Turn TV off via WebOS API.

```bash
homelab tv off
```

### `tv apps`
List all installed apps on your TV.

```bash
homelab tv apps
```

Shows app names and IDs. Use the ID with `tv launch` or `tv on --app`.

### `tv launch <app>`
Launch an app on the TV.

```bash
homelab tv launch netflix
homelab tv launch youtube
homelab tv launch com.disney.disneyplus-prod
```

Supports partial name matching - `netflix` will find the Netflix app.

### `tv key <key>`
Send a remote control key to the TV.

```bash
homelab tv key enter    # Press OK/Enter
homelab tv key back     # Go back
homelab tv key play     # Play media
homelab tv key pause    # Pause media
```

**Available Keys:**
| Key | Description |
|-----|-------------|
| `ENTER` / `OK` | Select/Confirm |
| `BACK` | Go back |
| `HOME` | Home screen |
| `UP`, `DOWN`, `LEFT`, `RIGHT` | Navigation |
| `PLAY`, `PAUSE`, `STOP` | Media controls |
| `VOLUMEUP`, `VOLUMEDOWN`, `MUTE` | Volume |
| `CHANNELUP`, `CHANNELDOWN` | Channels |
| `0`-`9` | Number keys |
| `RED`, `GREEN`, `YELLOW`, `BLUE` | Color buttons |

### `tv debug`
Debug TV connection and app detection.

```bash
homelab tv debug
```

Shows current foreground app and connection status.

---

## Use Cases

### Leave TV on for Pets

Turn on TV, launch your IPTV app, and start playing:

```bash
homelab tv on --app default --key enter
```

Set your IPTV app as default in `~/.homelab/tv.json`:
```json
{
  "DefaultApp": "cz.sledovanitv.rikplus"
}
```

### Quick Netflix

```bash
homelab tv on --app netflix
```

### Remote Control from Terminal

```bash
# Navigate menu
homelab tv key up
homelab tv key down
homelab tv key enter

# Control playback
homelab tv key play
homelab tv key pause

# Adjust volume
homelab tv key volumeup
homelab tv key mute
```

---

## Troubleshooting

### "TV not configured"
Run `homelab tv setup` first.

### "TV not paired"
Run `homelab tv setup` and accept the pairing prompt on your TV.

### Pairing prompt doesn't appear
1. Make sure TV is ON (not in standby)
2. Enable "LG Connect Apps" in TV settings
3. Try with `--verbose` flag: `homelab tv setup --verbose`

### Keys not working
1. Check app is fully loaded (try longer delay)
2. Use `--verbose` to debug: `homelab tv key enter --verbose`
3. Try `ENTER` vs `OK` - some apps respond to one or the other

### Connection timeout
1. Check TV is on the same network
2. Verify IP address: `ping <tv-ip>`
3. Check WebSocket ports: `nc -zv <tv-ip> 3000`

---

## Configuration

Config file: `~/.homelab/tv.json`

```json
{
  "Name": "Living Room TV",
  "IpAddress": "192.168.1.102",
  "MacAddress": "AA:BB:CC:DD:EE:FF",
  "ClientKey": "auto-generated-after-pairing",
  "Type": 0,
  "DefaultApp": "netflix"
}
```

| Field | Description |
|-------|-------------|
| `Name` | Friendly name for display |
| `IpAddress` | TV's IP address |
| `MacAddress` | TV's MAC address (for Wake-on-LAN) |
| `ClientKey` | Auto-generated pairing key |
| `Type` | TV type (0 = LG WebOS) |
| `DefaultApp` | App to launch with `--app default` |

---

## Requirements

- LG WebOS Smart TV (2016 or newer)
- TV connected to same network (or accessible via VPN)
- Wake-on-LAN enabled on TV (for `tv on`)
- "LG Connect Apps" enabled (for pairing)
