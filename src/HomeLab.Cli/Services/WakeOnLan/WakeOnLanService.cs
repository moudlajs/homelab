using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.WakeOnLan;

/// <summary>
/// Service for sending Wake-on-LAN magic packets.
/// </summary>
public class WakeOnLanService : IWakeOnLanService
{
    public async Task<bool> WakeAsync(string macAddress, string? broadcastAddress = null, int port = 9)
    {
        try
        {
            var macBytes = ParseMacAddress(macAddress);
            var magicPacket = BuildMagicPacket(macBytes);

            var targetAddress = broadcastAddress ?? "255.255.255.255";

            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            var endpoint = new IPEndPoint(IPAddress.Parse(targetAddress), port);
            await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);
            await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);
            await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsReachableAsync(string ipAddress, int timeoutMs = 3000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ParseMacAddress(string macAddress)
    {
        var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();

        if (cleanMac.Length != 12)
            throw new ArgumentException($"Invalid MAC address format: {macAddress}");

        var macBytes = new byte[6];
        for (int i = 0; i < 6; i++)
            macBytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);

        return macBytes;
    }

    private static byte[] BuildMagicPacket(byte[] macBytes)
    {
        var packet = new byte[6 + 16 * 6];
        for (int i = 0; i < 6; i++)
            packet[i] = 0xFF;
        for (int i = 0; i < 16; i++)
            Array.Copy(macBytes, 0, packet, 6 + i * 6, 6);
        return packet;
    }
}
