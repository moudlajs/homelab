using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using QRCoder;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of WireGuard client for testing.
/// </summary>
public class MockWireGuardClient : IWireGuardClient
{
    private readonly List<VpnPeer> _mockPeers = new()
    {
        new VpnPeer
        {
            Name = "danny-laptop",
            PublicKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmno=",
            AllowedIPs = "10.8.0.2/32",
            LastHandshake = DateTime.UtcNow.AddMinutes(-5),
            BytesReceived = 1024 * 1024 * 150,
            BytesSent = 1024 * 1024 * 75,
            IsActive = true
        },
        new VpnPeer
        {
            Name = "danny-phone",
            PublicKey = "ZYXWVUTSRQPONMLKJIHGFEDCBAzyxwvutsrqponml=",
            AllowedIPs = "10.8.0.3/32",
            LastHandshake = DateTime.UtcNow.AddHours(-2),
            BytesReceived = 1024 * 1024 * 25,
            BytesSent = 1024 * 1024 * 10,
            IsActive = false
        }
    };

    public string ServiceName => "WireGuard (Mock)";

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        return Task.FromResult(new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = "Running",
            Message = "Mock service - always healthy",
            Metrics = new Dictionary<string, string>
            {
                { "Active Peers", _mockPeers.Count(p => p.IsActive).ToString() },
                { "Total Peers", _mockPeers.Count.ToString() }
            }
        });
    }

    public Task<List<VpnPeer>> GetPeersAsync()
    {
        return Task.FromResult(_mockPeers.ToList());
    }

    public Task<string> AddPeerAsync(string name)
    {
        var peer = new VpnPeer
        {
            Name = name,
            PublicKey = $"MOCK{Guid.NewGuid():N}=",
            AllowedIPs = $"10.8.0.{_mockPeers.Count + 2}/32",
            IsActive = false
        };

        _mockPeers.Add(peer);

        var config = $@"[Interface]
PrivateKey = MOCK_PRIVATE_KEY_{name}
Address = {peer.AllowedIPs}
DNS = 10.8.0.1

[Peer]
PublicKey = SERVER_PUBLIC_KEY
Endpoint = homelab.example.com:51820
AllowedIPs = 10.8.0.0/24
PersistentKeepalive = 25";

        return Task.FromResult(config);
    }

    public Task RemovePeerAsync(string name)
    {
        var peer = _mockPeers.FirstOrDefault(p => p.Name == name);
        if (peer != null)
        {
            _mockPeers.Remove(peer);
        }
        return Task.CompletedTask;
    }

    public Task<byte[]> GenerateQRCodeAsync(string peerConfig)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(peerConfig, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return Task.FromResult(qrCodeImage);
    }
}
