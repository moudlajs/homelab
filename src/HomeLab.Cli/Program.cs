using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using HomeLab.Cli.Commands;
using HomeLab.Cli.Commands.Dns;
using HomeLab.Cli.Commands.Monitor;
using HomeLab.Cli.Commands.Vpn;
using HomeLab.Cli.Commands.Remote;
using HomeLab.Cli.Commands.Uptime;
using HomeLab.Cli.Commands.Speedtest;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.ServiceDiscovery;
using HomeLab.Cli.Services.Output;

namespace HomeLab.Cli;

/// <summary>
/// Entry point for the HomeLab CLI application.
/// This uses Spectre.Console.Cli to handle command routing.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // Setup dependency injection container
        var services = new ServiceCollection();

        // Phase 1-4 services
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();

        // Phase 5 services - Configuration and service clients
        services.AddSingleton<IHomelabConfigService, HomelabConfigService>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IServiceClientFactory, ServiceClientFactory>();

        // Phase 5 - Day 2: Service discovery and health checks
        services.AddSingleton<IServiceDiscoveryService, ServiceDiscoveryService>();
        services.AddSingleton<IServiceHealthCheckService, ServiceHealthCheckService>();

        // Phase 6: Output formatting
        services.AddSingleton<IOutputFormatter, OutputFormatter>();

        // Create registrar to connect Spectre with DI
        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            // Add description that shows in help text
            config.SetApplicationName("homelab");

            // Register commands (with aliases for convenience)
            config.AddCommand<StatusCommand>("status")
                .WithAlias("st")
                .WithDescription("Display homelab status dashboard");

            config.AddCommand<ServiceCommand>("service")
                .WithAlias("svc")
                .WithDescription("Manage service lifecycle (start, stop, restart)");

            config.AddCommand<ConfigCommand>("config")
                .WithDescription("Manage configuration (view, edit, backup, restore)");

            config.AddCommand<LogsCommand>("logs")
                .WithDescription("View container logs");

            config.AddCommand<UpdateCommand>("update")
                .WithDescription("Update container images");

            config.AddCommand<CleanupCommand>("cleanup")
                .WithDescription("Clean up unused Docker resources");

            config.AddCommand<TuiCommand>("tui")
                .WithAlias("ui")
                .WithAlias("dashboard")
                .WithDescription("Live dashboard (Terminal UI mode)");

            // Phase 5 - Day 3: VPN Management
            config.AddBranch("vpn", vpn =>
            {
                vpn.SetDescription("Manage VPN peers and configuration");
                vpn.AddCommand<VpnStatusCommand>("status")
                    .WithAlias("ls")
                    .WithAlias("list")
                    .WithDescription("Display VPN peer status");
                vpn.AddCommand<VpnAddPeerCommand>("add-peer")
                    .WithAlias("add")
                    .WithDescription("Add a new VPN peer");
                vpn.AddCommand<VpnRemovePeerCommand>("remove-peer")
                    .WithAlias("rm")
                    .WithAlias("remove")
                    .WithDescription("Remove a VPN peer");
            });

            // Phase 5 - Day 4: DNS Management
            config.AddBranch("dns", dns =>
            {
                dns.SetDescription("Manage DNS and ad-blocking");
                dns.AddCommand<DnsStatsCommand>("stats")
                    .WithAlias("st")
                    .WithDescription("Display DNS statistics");
                dns.AddCommand<DnsBlockedCommand>("blocked")
                    .WithAlias("bl")
                    .WithDescription("Display recently blocked domains");
            });

            // Phase 5 - Day 4: Monitoring
            config.AddBranch("monitor", monitor =>
            {
                monitor.SetDescription("Monitor homelab metrics and alerts");
                monitor.AddCommand<MonitorAlertsCommand>("alerts")
                    .WithAlias("al")
                    .WithDescription("Display active Prometheus alerts");
                monitor.AddCommand<MonitorTargetsCommand>("targets")
                    .WithAlias("tg")
                    .WithDescription("Display Prometheus scrape targets");
                monitor.AddCommand<MonitorDashboardCommand>("dashboard")
                    .WithAlias("dash")
                    .WithAlias("db")
                    .WithDescription("Open Grafana dashboards");
            });

            // Phase 5 - Day 6: Remote Management
            config.AddBranch("remote", remote =>
            {
                remote.SetDescription("Manage remote homelab connections");
                remote.AddCommand<RemoteConnectCommand>("connect")
                    .WithDescription("Add or update a remote connection");
                remote.AddCommand<RemoteListCommand>("list")
                    .WithDescription("List all configured remote connections");
                remote.AddCommand<RemoteStatusCommand>("status")
                    .WithDescription("Check status of remote homelab");
                remote.AddCommand<RemoteSyncCommand>("sync")
                    .WithDescription("Sync docker-compose files with remote");
                remote.AddCommand<RemoteRemoveCommand>("remove")
                    .WithDescription("Remove a remote connection");
            });

            // Phase 6: Uptime Monitoring
            config.AddBranch("uptime", uptime =>
            {
                uptime.SetDescription("Monitor service uptime and availability");
                uptime.AddCommand<UptimeStatusCommand>("status")
                    .WithAlias("st")
                    .WithAlias("ls")
                    .WithDescription("Display uptime monitoring status");
                uptime.AddCommand<UptimeAlertsCommand>("alerts")
                    .WithAlias("al")
                    .WithDescription("Show recent uptime alerts and incidents");
                uptime.AddCommand<UptimeAddCommand>("add")
                    .WithDescription("Add a new service to monitor");
            });

            // Phase 6: Internet Speed Testing
            config.AddBranch("speedtest", speedtest =>
            {
                speedtest.SetDescription("Monitor internet connection speed");
                speedtest.AddCommand<SpeedtestRunCommand>("run")
                    .WithDescription("Run a new speed test");
                speedtest.AddCommand<SpeedtestStatsCommand>("stats")
                    .WithAlias("st")
                    .WithDescription("Display speed test statistics and history");
            });
        });

        return app.Run(args);
    }
}

/// <summary>
/// Bridges Spectre.Console.Cli with Microsoft.Extensions.DependencyInjection.
/// This is boilerplate - just copy it.
/// </summary>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceCollection services)
        => _services = services;

    public void Register(Type service, Type implementation)
        => _services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => _services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => _services.AddSingleton(service, _ => factory());

    public ITypeResolver Build()
        => new TypeResolver(_services.BuildServiceProvider());
}

/// <summary>
/// Resolves types from the DI container.
/// This is boilerplate - just copy it.
/// </summary>
public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
        => _provider = provider;

    public object? Resolve(Type? type)
        => type != null ? _provider.GetService(type) : null;
}
