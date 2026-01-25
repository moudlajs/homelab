using HomeLab.Cli.Commands;
using HomeLab.Cli.Commands.Dns;
using HomeLab.Cli.Commands.HomeAssistant;
using HomeLab.Cli.Commands.Monitor;
using HomeLab.Cli.Commands.Network;
using HomeLab.Cli.Commands.Quick;
using HomeLab.Cli.Commands.Remote;
using HomeLab.Cli.Commands.Speedtest;
using HomeLab.Cli.Commands.Traefik;
using HomeLab.Cli.Commands.Tv;
using HomeLab.Cli.Commands.Uptime;
using HomeLab.Cli.Commands.Vpn;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Network;
using HomeLab.Cli.Services.Output;
using HomeLab.Cli.Services.ServiceDiscovery;
using HomeLab.Cli.Services.Update;
using HomeLab.Cli.Services.WakeOnLan;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

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

        // Phase 10: Self-update service
        services.AddSingleton<IGitHubReleaseService, GitHubReleaseService>();

        // Network monitoring services
        services.AddSingleton<INmapService, NmapService>();

        // TV control services
        services.AddSingleton<IWakeOnLanService, WakeOnLanService>();

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

            config.AddCommand<ImageUpdateCommand>("image-update")
                .WithDescription("Update container images");

            config.AddCommand<CleanupCommand>("cleanup")
                .WithDescription("Clean up unused Docker resources");

            // Phase 10: Version and self-update commands
            config.AddCommand<VersionCommand>("version")
                .WithDescription("Display version information");

            config.AddCommand<SelfUpdateCommand>("self-update")
                .WithDescription("Update HomeLab CLI to the latest version");

            config.AddCommand<TuiCommand>("tui")
                .WithAlias("ui")
                .WithAlias("dashboard")
                .WithDescription("Live dashboard (Terminal UI mode)");

            // Phase 5 - Day 3: VPN Management
            config.AddBranch("vpn", vpn =>
            {
                vpn.SetDescription("Manage VPN peers and configuration");
                vpn.AddCommand<VpnSetupCommand>("setup")
                    .WithDescription("Interactive wizard to set up VPN server");
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
                uptime.AddCommand<UptimeRemoveCommand>("remove")
                    .WithAlias("rm")
                    .WithDescription("Remove a monitor from tracking");
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

            // Phase 8: Home Assistant Integration
            config.AddBranch("ha", ha =>
            {
                ha.SetDescription("Control Home Assistant smart home devices");
                ha.AddCommand<HaStatusCommand>("status")
                    .WithAlias("st")
                    .WithAlias("ls")
                    .WithDescription("Display all Home Assistant entities");
                ha.AddCommand<HaControlCommand>("control")
                    .WithDescription("Control devices (on, off, toggle)");
                ha.AddCommand<HaGetCommand>("get")
                    .WithDescription("Get details of a specific entity");
                ha.AddCommand<HaListCommand>("list")
                    .WithDescription("List entities by domain (light, switch, sensor, etc.)");
            });

            // Phase 11: Traefik Reverse Proxy
            config.AddBranch("traefik", traefik =>
            {
                traefik.SetDescription("Manage Traefik reverse proxy");
                traefik.AddCommand<TraefikStatusCommand>("status")
                    .WithAlias("st")
                    .WithDescription("Display Traefik overview and status");
                traefik.AddCommand<TraefikRoutesCommand>("routes")
                    .WithDescription("List all HTTP routers");
                traefik.AddCommand<TraefikServicesCommand>("services")
                    .WithDescription("List all backend services");
                traefik.AddCommand<TraefikMiddlewaresCommand>("middlewares")
                    .WithAlias("mw")
                    .WithDescription("List all middlewares");
            });

            // Network Monitoring - Phase 1-3: Scanning, Traffic, Intrusion Detection
            config.AddBranch("network", network =>
            {
                network.SetDescription("Network scanning, monitoring, and security");
                network.AddCommand<NetworkScanCommand>("scan")
                    .WithDescription("Discover devices on network");
                network.AddCommand<NetworkPortsCommand>("ports")
                    .WithDescription("Scan ports on devices");
                network.AddCommand<NetworkDevicesCommand>("devices")
                    .WithDescription("List tracked network devices (ntopng)");
                network.AddCommand<NetworkTrafficCommand>("traffic")
                    .WithDescription("Display network traffic statistics");
                network.AddCommand<NetworkIntrusionCommand>("intrusion")
                    .WithAlias("alerts")
                    .WithDescription("Display security alerts (Suricata IDS)");
                network.AddCommand<NetworkStatusCommand>("status")
                    .WithAlias("st")
                    .WithDescription("Comprehensive network health overview");
            });

            // TV Control - LG WebOS Smart TV
            config.AddBranch("tv", tv =>
            {
                tv.SetDescription("Control LG WebOS Smart TV");
                tv.AddCommand<TvStatusCommand>("status")
                    .WithAlias("st")
                    .WithDescription("Check TV status and connectivity");
                tv.AddCommand<TvOnCommand>("on")
                    .WithDescription("Turn TV on via Wake-on-LAN");
                tv.AddCommand<TvOffCommand>("off")
                    .WithDescription("Turn TV off via WebOS API");
                tv.AddCommand<TvSetupCommand>("setup")
                    .WithDescription("Configure and pair with TV");
                tv.AddCommand<TvAppsCommand>("apps")
                    .WithDescription("List installed apps on TV");
                tv.AddCommand<TvLaunchCommand>("launch")
                    .WithDescription("Launch an app on TV");
                tv.AddCommand<TvKeyCommand>("key")
                    .WithDescription("Send remote control key to TV");
                tv.AddCommand<TvDebugCommand>("debug")
                    .WithDescription("Debug TV connection and app detection");
            });

            // Phase 7: Quick Actions - Fast operations for daily use
            config.AddCommand<QuickRestartCommand>("quick-restart")
                .WithAlias("qr")
                .WithDescription("Quick restart a service (fast, no confirmation)");

            config.AddCommand<QuickUpdateCommand>("quick-update")
                .WithAlias("qu")
                .WithDescription("Quick update service (pull + restart)");

            config.AddCommand<QuickBackupCommand>("quick-backup")
                .WithAlias("qb")
                .WithDescription("Quick backup container configs");

            config.AddCommand<QuickFixCommand>("quick-fix")
                .WithAlias("qf")
                .WithDescription("Quick fix service (stop, clear cache, restart)");

            config.AddCommand<QuickDogTvCommand>("quick-dog-tv")
                .WithAlias("dog-tv")
                .WithAlias("dtv")
                .WithDescription("Quick turn on TV for your dog");

            // Shell completion
            config.AddCommand<CompletionCommand>("completion")
                .WithDescription("Generate shell completion scripts (bash, zsh)");
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
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }

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
