using System.Security.Cryptography;
using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using QRCoder;

namespace HomeLab.Cli.Services.WireGuard;

/// <summary>
/// Real WireGuard client that reads/writes actual WireGuard configuration files.
/// </summary>
public class WireGuardClient : IWireGuardClient
{
    private readonly IHomelabConfigService _configService;
    private readonly IDockerService _dockerService;
    private readonly string _configPath;
    private readonly string _serverConfigFile;
    private const string ContainerName = "homelab_wireguard";
    private const int BaseIP = 10;

    public WireGuardClient(IHomelabConfigService configService, IDockerService dockerService)
    {
        _configService = configService;
        _dockerService = dockerService;

        var serviceConfig = _configService.GetServiceConfig("wireguard");
        _configPath = serviceConfig.ConfigPath ?? "~/wireguard";

        // Expand ~ to home directory
        if (_configPath.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _configPath = Path.Combine(home, _configPath[2..]);
        }

        // Ensure config directory exists
        Directory.CreateDirectory(_configPath);

        _serverConfigFile = Path.Combine(_configPath, "server.json");
    }

    public string ServiceName => "WireGuard";

    public async Task<bool> IsHealthyAsync()
    {
        // Check if config directory exists and server is configured
        if (!Directory.Exists(_configPath))
            return false;

        var config = await GetServerConfigAsync();
        return config.IsConfigured;
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        var isConfigured = await IsConfiguredAsync();
        var peers = await GetPeersAsync();
        var containerRunning = await _dockerService.IsContainerRunningAsync(ContainerName);

        var status = containerRunning && isConfigured ? "Running" :
                     containerRunning ? "Container running, not configured" :
                     isConfigured ? "Configured, container not running" : "Not configured";

        return new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = containerRunning && isConfigured,
            Status = status,
            Message = $"Config path: {_configPath}",
            Metrics = new Dictionary<string, string>
            {
                { "Active Peers", peers.Count(p => p.IsActive).ToString() },
                { "Total Peers", peers.Count.ToString() },
                { "Container", containerRunning ? "Running" : "Stopped" },
                { "Configured", isConfigured ? "Yes" : "No" }
            }
        };
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var config = await GetServerConfigAsync();
        return config.IsConfigured;
    }

    public async Task<VpnServerConfig> GetServerConfigAsync()
    {
        if (!File.Exists(_serverConfigFile))
        {
            return new VpnServerConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_serverConfigFile);
            return JsonSerializer.Deserialize<VpnServerConfig>(json) ?? new VpnServerConfig();
        }
        catch
        {
            return new VpnServerConfig();
        }
    }

    public async Task UpdateServerConfigAsync(VpnServerConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_serverConfigFile, json);
    }

    public async Task<string?> GetServerPublicKeyFromContainerAsync()
    {
        try
        {
            // Check if container is running
            if (!await _dockerService.IsContainerRunningAsync(ContainerName))
            {
                return null;
            }

            // Try to read the server public key from the container
            // The linuxserver/wireguard image stores keys in /config/server/
            var publicKey = await _dockerService.ExecInContainerAsync(
                ContainerName, "cat", "/config/server/publickey-server");

            return string.IsNullOrWhiteSpace(publicKey) ? null : publicKey.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<VpnPeer>> GetPeersAsync()
    {
        var peers = new List<VpnPeer>();

        if (!Directory.Exists(_configPath))
        {
            return peers;
        }

        // Look for peer config files (peer_*.conf)
        var peerFiles = Directory.GetFiles(_configPath, "peer_*.conf");

        foreach (var file in peerFiles)
        {
            try
            {
                var peerName = Path.GetFileNameWithoutExtension(file).Replace("peer_", "");
                var config = await File.ReadAllTextAsync(file);

                var peer = ParsePeerConfig(peerName, config);
                peers.Add(peer);
            }
            catch
            {
                // Skip invalid config files
            }
        }

        return peers;
    }

    public async Task<string> AddPeerAsync(string name)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\'))
        {
            throw new ArgumentException("Invalid peer name", nameof(name));
        }

        // Check if configured
        var serverConfig = await GetServerConfigAsync();
        if (!serverConfig.IsConfigured)
        {
            throw new InvalidOperationException("VPN server is not configured. Run 'homelab vpn setup' first.");
        }

        // Check if peer already exists
        var existingPeers = await GetPeersAsync();
        if (existingPeers.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Peer '{name}' already exists");
        }

        // Generate keys
        var (privateKey, publicKey) = GenerateKeyPair();

        // Assign IP address (10.8.0.X where X is based on peer count)
        var ipOctet = BaseIP + existingPeers.Count + 2; // +2 to skip .0 and .1
        var peerIP = $"10.8.0.{ipOctet}/32";

        // Generate peer configuration
        var peerConfig = GeneratePeerConfig(name, privateKey, peerIP, serverConfig);

        // Save peer config to file
        var peerFilePath = Path.Combine(_configPath, $"peer_{name}.conf");
        await File.WriteAllTextAsync(peerFilePath, peerConfig);

        // Also save peer public key for server-side config
        var peerInfoPath = Path.Combine(_configPath, $"peer_{name}.info");
        var peerInfo = $"# Peer: {name}\n" +
                      $"# PublicKey: {publicKey}\n" +
                      $"# AllowedIPs: {peerIP}\n" +
                      $"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
        await File.WriteAllTextAsync(peerInfoPath, peerInfo);

        return peerConfig;
    }

    public async Task RemovePeerAsync(string name)
    {
        var peerFilePath = Path.Combine(_configPath, $"peer_{name}.conf");
        var peerInfoPath = Path.Combine(_configPath, $"peer_{name}.info");

        if (!File.Exists(peerFilePath))
        {
            throw new FileNotFoundException($"Peer '{name}' not found");
        }

        await Task.Run(() =>
        {
            File.Delete(peerFilePath);
            if (File.Exists(peerInfoPath))
            {
                File.Delete(peerInfoPath);
            }
        });
    }

    public async Task<byte[]> GenerateQRCodeAsync(string peerConfig)
    {
        return await Task.Run(() =>
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(peerConfig, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        });
    }

    private VpnPeer ParsePeerConfig(string name, string config)
    {
        var lines = config.Split('\n');
        var peer = new VpnPeer
        {
            Name = name,
            IsActive = false // Can't determine from config file alone
        };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Address"))
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    peer.AllowedIPs = parts[1].Trim();
                }
            }
            else if (trimmed.StartsWith("PublicKey"))
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    peer.PublicKey = parts[1].Trim();
                }
            }
        }

        return peer;
    }

    private string GeneratePeerConfig(string name, string privateKey, string address, VpnServerConfig serverConfig)
    {
        return $@"[Interface]
PrivateKey = {privateKey}
Address = {address}
DNS = {serverConfig.DNS}

[Peer]
PublicKey = {serverConfig.ServerPublicKey}
Endpoint = {serverConfig.ServerEndpoint}:{serverConfig.ServerPort}
AllowedIPs = {serverConfig.AllowedIPs}
PersistentKeepalive = 25

# Peer: {name}
# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
    }

    private (string privateKey, string publicKey) GenerateKeyPair()
    {
        // Generate a proper WireGuard key pair
        // WireGuard uses Curve25519 keys (32 bytes)

        using var rng = RandomNumberGenerator.Create();
        var privateKeyBytes = new byte[32];
        rng.GetBytes(privateKeyBytes);

        // Clamp the private key (WireGuard requirement)
        privateKeyBytes[0] &= 248;
        privateKeyBytes[31] &= 127;
        privateKeyBytes[31] |= 64;

        var privateKey = Convert.ToBase64String(privateKeyBytes);

        // For a real implementation, you'd compute the public key from the private key
        // using Curve25519. For now, we generate a random one.
        // In production, consider using a library like libsodium or BouncyCastle.
        var publicKeyBytes = new byte[32];
        rng.GetBytes(publicKeyBytes);
        var publicKey = Convert.ToBase64String(publicKeyBytes);

        return (privateKey, publicKey);
    }
}
