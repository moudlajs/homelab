# Self-Update Guide

The HomeLab CLI can update itself from GitHub Releases.

---

## Commands

### Check for updates
```bash
homelab self-update --check
```
Shows current vs latest version without installing anything.

### Update to latest
```bash
homelab self-update
```
Downloads and installs the latest release. Asks for confirmation before proceeding.

### Update to specific version
```bash
homelab self-update --version 1.8.0
```

### Skip confirmation
```bash
homelab self-update --force
```

---

## How It Works

1. Fetches latest release info from GitHub API (`moudlajs/homelab`)
2. Compares versions (strips `v` prefix and git hash suffix)
3. Finds the correct binary for your platform (e.g., `macos-arm64`)
4. Downloads with a progress bar showing speed and ETA
5. Backs up the current binary
6. Installs the new binary
7. Signs it (macOS only — clears quarantine + ad-hoc codesign)
8. Verifies the new binary runs (`homelab version`)
9. Cleans up temp and backup files

---

## Crash Safety

The updater has a backup/verify/rollback mechanism:

| Step | What happens on failure |
|------|------------------------|
| Download | Temp file cleaned up, no changes made |
| Download too small (<1KB) | Rejected as corrupt, no changes made |
| File copy | Backup exists, original untouched if copy throws |
| Code signing (macOS) | Rolls back to backup automatically |
| Verification (`homelab version`) | Rolls back to backup automatically |
| Rollback itself fails | Tells you where the `.bak` file is for manual recovery |

### Manual recovery

If everything goes wrong and the binary is broken:
```bash
# If backup exists
cp ~/.local/bin/homelab.bak ~/.local/bin/homelab
xattr -cr ~/.local/bin/homelab
codesign -f -s - ~/.local/bin/homelab

# Or reinstall from source
cd ~/Repos/homelab
dotnet publish src/HomeLab.Cli/HomeLab.Cli.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true -o publish-single
cp publish-single/HomeLab.Cli ~/.local/bin/homelab
xattr -cr ~/.local/bin/homelab
codesign -f -s - ~/.local/bin/homelab
```

---

## Install Path Detection

The updater auto-detects where the binary is installed by checking `Process.GetCurrentProcess().MainModule.FileName`. Falls back to `~/.local/bin/homelab`.

- `~/.local/bin/` — direct copy, no sudo
- `/usr/local/bin/` — uses sudo

---

## Platform Support

| Platform | Binary suffix |
|----------|--------------|
| macOS ARM64 (M1/M2/M4) | `macos-arm64` |
| macOS x64 | `macos-x64` |
| Linux x64 | `linux-x64` |
| Linux ARM64 | `linux-arm64` |
| Windows x64 | `win-x64` |
| Windows ARM64 | `win-arm64` |

Currently only `osx-arm64` is built in CI. Other platforms can be added to the release workflow.

---

## Version Display

```bash
homelab version
```

Shows product name, version (without git hash), .NET runtime, and platform.
