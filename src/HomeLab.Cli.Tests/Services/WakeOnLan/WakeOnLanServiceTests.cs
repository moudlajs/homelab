using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using HomeLab.Cli.Services.WakeOnLan;
using Xunit;

namespace HomeLab.Cli.Tests.Services.WakeOnLan;

public class WakeOnLanServiceTests
{
    private readonly WakeOnLanService _sut = new();

    [Fact]
    public async Task IsPortOpenAsync_ListeningPort_ReturnsTrue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var result = await _sut.IsPortOpenAsync("127.0.0.1", port, 2000);

            result.Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task IsPortOpenAsync_ClosedPort_ReturnsFalse()
    {
        // Bind then release, so the port is known to be free.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var result = await _sut.IsPortOpenAsync("127.0.0.1", port, 2000);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPortOpenAsync_UnroutableAddress_ReturnsFalseWithoutThrowing()
    {
        // 192.0.2.0/24 is TEST-NET-1 (RFC 5737) and is never routable.
        var result = await _sut.IsPortOpenAsync("192.0.2.1", 3000, 500);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPortOpenAsync_MalformedHost_ReturnsFalseWithoutThrowing()
    {
        var result = await _sut.IsPortOpenAsync("not a host", 3000, 500);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("74:E6:B8:62:42:04")]
    [InlineData("74-E6-B8-62-42-04")]
    [InlineData("74e6b8624204")]
    public async Task WakeAsync_AcceptedMacFormats_ReturnsTrue(string mac)
    {
        var result = await _sut.WakeAsync(mac, "127.0.0.1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WakeAsync_InvalidMac_ReturnsFalse()
    {
        var result = await _sut.WakeAsync("nope", "127.0.0.1");

        result.Should().BeFalse();
    }
}
