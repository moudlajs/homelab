using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Mocks;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Factory that creates service clients based on configuration.
/// Returns mock clients if development.use_mock_services = true.
/// </summary>
public class ServiceClientFactory : IServiceClientFactory
{
    private readonly IHomelabConfigService _configService;
    private readonly HttpClient _httpClient;

    public ServiceClientFactory(IHomelabConfigService configService, HttpClient httpClient)
    {
        _configService = configService;
        _httpClient = httpClient;
    }

    public IAdGuardClient CreateAdGuardClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockAdGuardClient();
        }

        // TODO: Create real AdGuardClient in Day 4
        throw new NotImplementedException("Real AdGuardClient not yet implemented. Set development.use_mock_services = true in config.");
    }

    public IWireGuardClient CreateWireGuardClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockWireGuardClient();
        }

        // TODO: Create real WireGuardClient in Day 3
        throw new NotImplementedException("Real WireGuardClient not yet implemented. Set development.use_mock_services = true in config.");
    }

    public IPrometheusClient CreatePrometheusClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockPrometheusClient();
        }

        // TODO: Create real PrometheusClient in Day 4
        throw new NotImplementedException("Real PrometheusClient not yet implemented. Set development.use_mock_services = true in config.");
    }

    public IGrafanaClient CreateGrafanaClient()
    {
        if (_configService.UseMockServices)
        {
            return new MockGrafanaClient();
        }

        // TODO: Create real GrafanaClient in Day 4
        throw new NotImplementedException("Real GrafanaClient not yet implemented. Set development.use_mock_services = true in config.");
    }
}
