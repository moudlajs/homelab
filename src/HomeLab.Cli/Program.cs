using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using HomeLab.Cli.Commands;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Configuration;

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
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IConfigService, ConfigService>();

        // Create registrar to connect Spectre with DI
        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            // Add description that shows in help text
            config.SetApplicationName("homelab");

            // Register commands
            config.AddCommand<StatusCommand>("status")
                .WithDescription("Display homelab status dashboard");

            config.AddCommand<ServiceCommand>("service")
                .WithDescription("Manage service lifecycle (start, stop, restart)");

            config.AddCommand<ConfigCommand>("config")
                .WithDescription("Manage configuration (view, edit, backup, restore)");
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
