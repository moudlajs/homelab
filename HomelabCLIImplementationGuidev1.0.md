# Homelab CLI Implementation Guide
## Complete Guide to Building a Mac Mini M4 Homelab Controller with Spectre.Console

**Version:** 1.0  
**Target Platform:** macOS (Mac Mini M4)  
**Language:** C# (.NET 8)  
**Framework:** Spectre.Console  
**Development:** JetBrains Rider

---

## Table of Contents

1. [Introduction & Philosophy](#introduction--philosophy)
2. [Spectre.Console 101](#spectreconsole-101)
3. [Architecture Overview](#architecture-overview)
4. [Project Structure](#project-structure)
5. [Phase 1: Foundation & Status Dashboard](#phase-1-foundation--status-dashboard)
6. [Phase 2: Service Lifecycle Control](#phase-2-service-lifecycle-control)
7. [Phase 3: Configuration Management](#phase-3-configuration-management)
8. [Phase 4: Maintenance & Automation](#phase-4-maintenance--automation)
9. [Testing Strategy](#testing-strategy)
10. [Deployment & Distribution](#deployment--distribution)

---

## Introduction & Philosophy

### What We're Building

A **command-line interface (CLI)** that acts as the central nervous system for your homelab. Think of it as "mission control" for all your services running on Mac Mini M4.

### Why CLI Instead of Web UI?

**Advantages:**
- **Speed**: Instant feedback, no HTTP latency
- **Scriptable**: Can be automated in bash/cron
- **SSH-friendly**: Works over remote connections
- **Low overhead**: No web server needed
- **Terminal workflow**: Fits DevOps culture

### Core Principles

1. **KISS (Keep It Simple, Stupid)**
    - Don't over-engineer
    - Solve today's problems, not imaginary future ones

2. **DRY (Don't Repeat Yourself)**
    - Shared logic in services
    - Reusable components

3. **Separation of Concerns**
    - Commands handle UI
    - Services handle business logic
    - Models handle data

4. **Fail Fast**
    - Validate early
    - Clear error messages
    - No silent failures

---

## Spectre.Console 101

### What Is Spectre.Console?

**Spectre.Console** is a .NET library for creating beautiful console applications. It's like Bootstrap for the terminal.

**Official Docs:** https://spectreconsole.net/

### Why Spectre.Console?

| Without Spectre | With Spectre |
|----------------|--------------|
| `Console.WriteLine("Status: OK")` | Rich colored tables, progress bars, trees |
| Manual color codes | Automatic styling with markup |
| Ugly, hard to read | Beautiful, organized output |

### Installation

```bash
dotnet add package Spectre.Console
```

### Basic Concepts

#### 1. **Markup** - Colored Text

```csharp
using Spectre.Console;

// Simple colored text
AnsiConsole.MarkupLine("[green]Success![/]");
AnsiConsole.MarkupLine("[red]Error![/]");

// Multiple colors
AnsiConsole.MarkupLine("[yellow]Warning:[/] [blue]System restarting[/]");
```

**Why?** Better than manually doing `Console.ForegroundColor = ConsoleColor.Green;`

#### 2. **Tables** - Organized Data

```csharp
var table = new Table();
table.AddColumn("Service");
table.AddColumn("Status");
table.AddColumn("Uptime");

table.AddRow("AdGuard", "[green]Running[/]", "3d 5h");
table.AddRow("WireGuard", "[green]Running[/]", "3d 5h");
table.AddRow("Grafana", "[red]Stopped[/]", "N/A");

AnsiConsole.Write(table);
```

**Output:**
```
┌───────────┬─────────┬─────────┐
│ Service   │ Status  │ Uptime  │
├───────────┼─────────┼─────────┤
│ AdGuard   │ Running │ 3d 5h   │
│ WireGuard │ Running │ 3d 5h   │
│ Grafana   │ Stopped │ N/A     │
└───────────┴─────────┴─────────┘
```

**Why?** Instantly readable vs. plain text dumps.

#### 3. **Prompts** - Interactive Input

```csharp
// Simple confirmation
var confirm = AnsiConsole.Confirm("Restart Grafana?");
if (confirm) {
    // Do restart
}

// Selection from list
var service = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Which service to restart?")
        .AddChoices("AdGuard", "WireGuard", "Grafana")
);
```

**Why?** Built-in validation, arrow-key navigation, clean UX.

#### 4. **Progress Bars** - Long Operations

```csharp
await AnsiConsole.Progress()
    .StartAsync(async ctx => 
    {
        var task = ctx.AddTask("Backing up...");
        
        while (!task.IsFinished)
        {
            await Task.Delay(250);
            task.Increment(5);
        }
    });
```

**Output:**
```
Backing up... ███████████████░░░░░ 75%
```

**Why?** Users know something is happening, not frozen.

#### 5. **Panels** - Grouped Information

```csharp
var panel = new Panel("[green]All systems operational[/]")
{
    Header = new PanelHeader("🏠 Homelab Status"),
    Border = BoxBorder.Rounded
};

AnsiConsole.Write(panel);
```

**Output:**
```
╭─ 🏠 Homelab Status ─────────────╮
│ All systems operational         │
╰─────────────────────────────────╯
```

**Why?** Visual grouping makes dashboards scannable.

### Spectre.Console.Cli - Command Framework

Spectre also has a **command framework** (like `git <command> <args>`):

```csharp
var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<StatusCommand>("status");
    config.AddCommand<ServiceCommand>("service");
});

return app.Run(args);
```

**Usage:**
```bash
homelab status
homelab service start adguard
homelab service stop --all
```

**Why?** Handles argument parsing, validation, help text automatically.

---

## Architecture Overview

### Layered Architecture (Clean Code)

```
┌─────────────────────────────────────────┐
│         CLI Layer (Commands)            │  ← User interaction
│   StatusCommand, ServiceCommand, etc.   │
├─────────────────────────────────────────┤
│       Business Logic (Services)         │  ← Core functionality
│  DockerService, BackupService, etc.     │
├─────────────────────────────────────────┤
│           Models (DTOs)                 │  ← Data structures
│  ServiceStatus, HealthCheck, etc.       │
├─────────────────────────────────────────┤
│      Infrastructure (External)          │  ← 3rd party integrations
│  Docker SDK, File system, HTTP calls    │
└─────────────────────────────────────────┘
```

### Why This Structure?

**Testability:**
- Can test `DockerService` without CLI
- Can mock `DockerService` in command tests

**Maintainability:**
- Change Docker implementation without touching commands
- Add new commands without touching services

**Clarity:**
- Each layer has one responsibility
- Easy to navigate codebase

---

## Project Structure

### Directory Layout

```
homelab-cli/
├── src/
│   ├── HomelabCli/                          # Main console app
│   │   ├── Commands/                        # CLI commands (UI layer)
│   │   │   ├── StatusCommand.cs
│   │   │   ├── ServiceCommand.cs
│   │   │   ├── ConfigCommand.cs
│   │   │   └── BackupCommand.cs
│   │   │
│   │   ├── Services/                        # Business logic
│   │   │   ├── Docker/
│   │   │   │   ├── IDockerService.cs        # Interface (for testing)
│   │   │   │   └── DockerService.cs         # Implementation
│   │   │   ├── Backup/
│   │   │   │   ├── IBackupService.cs
│   │   │   │   └── BackupService.cs
│   │   │   └── Health/
│   │   │       ├── IHealthCheckService.cs
│   │   │       └── HealthCheckService.cs
│   │   │
│   │   ├── Models/                          # Data structures
│   │   │   ├── ServiceStatus.cs
│   │   │   ├── ContainerInfo.cs
│   │   │   ├── HealthCheckResult.cs
│   │   │   └── BackupMetadata.cs
│   │   │
│   │   ├── Infrastructure/                  # External integrations
│   │   │   ├── Docker/
│   │   │   │   └── DockerClientFactory.cs
│   │   │   ├── Configuration/
│   │   │   │   └── CliConfiguration.cs
│   │   │   └── Shell/
│   │   │       └── ShellExecutor.cs         # For calling bash scripts
│   │   │
│   │   ├── Program.cs                       # Entry point
│   │   └── HomelabCli.csproj
│   │
│   └── HomelabCli.Tests/                    # Unit tests
│       ├── Commands/
│       ├── Services/
│       └── HomelabCli.Tests.csproj
│
├── config/
│   └── homelab-cli.yaml                     # CLI configuration
│
├── scripts/
│   └── install.sh                           # Installation helper
│
├── docs/
│   └── ARCHITECTURE.md                      # This file
│
├── .gitignore
├── README.md
└── homelab-cli.sln                          # Solution file
```

### Why This Structure?

1. **Separation by Concern**: Commands, Services, Models, Infrastructure
2. **Testable**: Each layer can be unit tested independently
3. **Scalable**: Easy to add new commands/services
4. **Standard .NET**: Follows C# project conventions

---

## Phase 1: Foundation & Status Dashboard

### Goals

- ✅ Setup project structure
- ✅ Implement `homelab status` command
- ✅ Show running containers
- ✅ Show system resource usage
- ✅ Basic health checks

### Step-by-Step Implementation

#### Step 1.1: Create Project

```bash
# Using Rider or terminal:
dotnet new console -n HomelabCli
cd HomelabCli
dotnet add package Spectre.Console
dotnet add package Docker.DotNet  # Official Docker SDK
dotnet add package YamlDotNet     # For parsing compose files
```

#### Step 1.2: Setup Program.cs (Entry Point)

**File:** `src/HomelabCli/Program.cs`

```csharp
using Spectre.Console.Cli;
using HomelabCli.Commands;

namespace HomelabCli;

/// <summary>
/// Entry point for the Homelab CLI application.
/// This uses Spectre.Console.Cli to handle command routing.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        
        app.Configure(config =>
        {
            // Add description that shows in help text
            config.SetApplicationName("homelab");
            
            // Register commands
            config.AddCommand<StatusCommand>("status")
                .WithDescription("Display homelab status dashboard");
                
            // More commands will be added in later phases
        });
        
        return app.Run(args);
    }
}
```

**Why This Structure?**

1. **`CommandApp`**: Spectre's command framework handles:
    - Argument parsing (`homelab status --verbose`)
    - Help text generation (`homelab --help`)
    - Command validation

2. **`config.AddCommand<T>("name")`**: Registers a command class
    - `<StatusCommand>`: The class that implements the command
    - `"status"`: The word users type (`homelab status`)

3. **Why static?**: Entry points in C# must be static

#### Step 1.3: Create StatusCommand

**File:** `src/HomelabCli/Commands/StatusCommand.cs`

```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using HomelabCli.Services.Docker;

namespace HomelabCli.Commands;

/// <summary>
/// Displays the homelab status dashboard.
/// Shows running containers, resource usage, and health checks.
/// </summary>
public class StatusCommand : AsyncCommand
{
    // Dependency injection - we'll explain this
    private readonly IDockerService _dockerService;
    
    public StatusCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        // Show a fancy header
        AnsiConsole.Write(
            new FigletText("Homelab Status")
                .Centered()
                .Color(Color.Green));
        
        // Get container info from Docker
        var containers = await _dockerService.ListContainersAsync();
        
        // Create a table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Container[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Uptime[/]");
        
        // Fill table with data
        foreach (var container in containers)
        {
            var statusColor = container.IsRunning ? "green" : "red";
            var statusText = container.IsRunning ? "Running" : "Stopped";
            
            table.AddRow(
                container.Name,
                $"[{statusColor}]{statusText}[/]",
                container.Uptime
            );
        }
        
        // Display the table
        AnsiConsole.Write(table);
        
        // Return 0 = success (Unix convention)
        return 0;
    }
}
```

**Explanation of Key Concepts:**

1. **`AsyncCommand`**: Base class from Spectre.Console.Cli
    - Why async? Docker API calls are network I/O
    - Prevents blocking the UI thread

2. **Constructor Injection**:
   ```csharp
   public StatusCommand(IDockerService dockerService)
   ```
    - **What?** Dependencies are passed in, not created inside
    - **Why?** Testability - can pass a mock `IDockerService`
    - **How?** We'll setup DI container later

3. **`ExecuteAsync`**: The actual command logic
    - Called when user types `homelab status`
    - Must return `int` (exit code)
    - `0` = success, non-zero = error

4. **`FigletText`**: ASCII art headers
   ```
   ╦ ╦┌─┐┌┬┐┌─┐┬  ┌─┐┌┐   ╔═╗┌┬┐┌─┐┌┬┐┬ ┬┌─┐
   ╠═╣│ ││││├┤ │  ├─┤├┴┐  ╚═╗ │ ├─┤ │ │ │└─┐
   ╩ ╩└─┘┴ ┴└─┘┴─┘┴ ┴└─┘  ╚═╝ ┴ ┴ ┴ ┴ └─┘└─┘
   ```

#### Step 1.4: Create IDockerService Interface

**File:** `src/HomelabCli/Services/Docker/IDockerService.cs`

```csharp
using HomelabCli.Models;

namespace HomelabCli.Services.Docker;

/// <summary>
/// Interface for Docker operations.
/// Using an interface allows us to mock this in tests.
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Lists all containers, optionally filtered to homelab namespace.
    /// </summary>
    Task<List<ContainerInfo>> ListContainersAsync(bool onlyHomelab = true);
    
    /// <summary>
    /// Starts a container by name.
    /// </summary>
    Task StartContainerAsync(string name);
    
    /// <summary>
    /// Stops a container by name.
    /// </summary>
    Task StopContainerAsync(string name);
}
```

**Why an Interface?**

1. **Testability**:
   ```csharp
   // In tests, we can use a fake:
   var mockDocker = new MockDockerService();
   var command = new StatusCommand(mockDocker);
   ```

2. **Flexibility**: Can swap implementations
    - `DockerService` - real Docker API
    - `RemoteDockerService` - SSH to Mac Mini
    - `MockDockerService` - for tests

3. **Dependency Inversion Principle (SOLID)**
    - High-level code (commands) don't depend on low-level details (Docker SDK)
    - Both depend on abstraction (interface)

#### Step 1.5: Create ContainerInfo Model

**File:** `src/HomelabCli/Models/ContainerInfo.cs`

```csharp
namespace HomelabCli.Models;

/// <summary>
/// Represents information about a Docker container.
/// This is a "DTO" (Data Transfer Object) - just data, no logic.
/// </summary>
public class ContainerInfo
{
    /// <summary>
    /// Container name (e.g., "homelab_adguard").
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Container ID (Docker's internal ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Is the container currently running?
    /// </summary>
    public bool IsRunning { get; set; }
    
    /// <summary>
    /// Human-readable uptime (e.g., "3 days").
    /// </summary>
    public string Uptime { get; set; } = "N/A";
    
    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double CpuPercent { get; set; }
    
    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public double MemoryMB { get; set; }
}
```

**Why a Separate Model?**

1. **Decoupling**: Docker SDK returns complex objects
    - We only extract what we need
    - If Docker API changes, only `DockerService` changes

2. **Simplicity**: Commands work with simple POCOs
    - No need to understand Docker's internal structures

3. **Testability**: Easy to create test data
   ```csharp
   var testContainer = new ContainerInfo 
   { 
       Name = "test", 
       IsRunning = true 
   };
   ```

#### Step 1.6: Implement DockerService

**File:** `src/HomelabCli/Services/Docker/DockerService.cs`

```csharp
using Docker.DotNet;
using Docker.DotNet.Models;
using HomelabCli.Models;

namespace HomelabCli.Services.Docker;

/// <summary>
/// Implementation of IDockerService using Docker.DotNet SDK.
/// This is where the actual Docker API calls happen.
/// </summary>
public class DockerService : IDockerService
{
    private readonly DockerClient _client;
    
    public DockerService()
    {
        // Create Docker client
        // On macOS, Docker socket is at unix:///var/run/docker.sock
        _client = new DockerClientConfiguration(
            new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }
    
    public async Task<List<ContainerInfo>> ListContainersAsync(bool onlyHomelab = true)
    {
        // Call Docker API to list containers
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters 
            { 
                All = true  // Include stopped containers
            });
        
        // Filter to homelab_ prefix if requested
        var filtered = onlyHomelab 
            ? containers.Where(c => c.Names.Any(n => n.Contains("homelab_")))
            : containers;
        
        // Convert Docker's data to our simple model
        return filtered.Select(c => new ContainerInfo
        {
            Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
            Id = c.ID,
            IsRunning = c.State == "running",
            Uptime = CalculateUptime(c.Created),
            // CPU/Memory require stats API (added later)
        }).ToList();
    }
    
    public async Task StartContainerAsync(string name)
    {
        // Find container by name
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });
            
        var container = containers.FirstOrDefault(c => 
            c.Names.Any(n => n.Contains(name)));
            
        if (container == null)
            throw new Exception($"Container '{name}' not found");
        
        // Start it
        await _client.Containers.StartContainerAsync(
            container.ID, 
            new ContainerStartParameters());
    }
    
    public async Task StopContainerAsync(string name)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });
            
        var container = containers.FirstOrDefault(c => 
            c.Names.Any(n => n.Contains(name)));
            
        if (container == null)
            throw new Exception($"Container '{name}' not found");
        
        await _client.Containers.StopContainerAsync(
            container.ID, 
            new ContainerStopParameters());
    }
    
    /// <summary>
    /// Helper method to calculate human-readable uptime.
    /// </summary>
    private string CalculateUptime(DateTime created)
    {
        var uptime = DateTime.UtcNow - created;
        
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        
        return $"{(int)uptime.TotalMinutes}m";
    }
}
```

**Key Concepts Explained:**

1. **`DockerClient`**: Official Docker SDK client
    - Connects via Unix socket (`/var/run/docker.sock`)
    - Same socket Docker CLI uses

2. **`async/await`**:
    - Docker API is network I/O (even locally)
    - Async prevents blocking the UI

3. **`LINQ`** (`.Where()`, `.Select()`):
    - Functional programming in C#
    - Cleaner than `for` loops

4. **Private Helper Methods**:
    - `CalculateUptime` - keeps main methods clean
    - KISS principle - small, focused functions

#### Step 1.7: Wire Up Dependency Injection

**Update:** `src/HomelabCli/Program.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using HomelabCli.Commands;
using HomelabCli.Services.Docker;

namespace HomelabCli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Setup dependency injection container
        var services = new ServiceCollection();
        services.AddSingleton<IDockerService, DockerService>();
        
        // Create registrar to connect Spectre with DI
        var registrar = new TypeRegistrar(services);
        
        var app = new CommandApp(registrar);
        
        app.Configure(config =>
        {
            config.SetApplicationName("homelab");
            config.AddCommand<StatusCommand>("status");
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
```

**What Just Happened?**

1. **Dependency Injection (DI) Container**:
    - `ServiceCollection` - list of services
    - When `StatusCommand` needs `IDockerService`, DI provides `DockerService`

2. **`AddSingleton`**:
    - Creates one instance, reused everywhere
    - Good for stateless services like `DockerService`

3. **`TypeRegistrar`**:
    - Connects Spectre's command system with .NET's DI
    - Boilerplate - just copy/paste this

#### Step 1.8: Test It!

```bash
# Build the project
dotnet build

# Run the status command
dotnet run -- status
```

**Expected Output:**
```
 ╦ ╦┌─┐┌┬┐┌─┐┬  ┌─┐┌┐   ╔═╗┌┬┐┌─┐┌┬┐┬ ┬┌─┐
 ╠═╣│ ││││├┤ │  ├─┤├┴┐  ╚═╗ │ ├─┤ │ │ │└─┐
 ╩ ╩└─┘┴ ┴└─┘┴─┘┴ ┴└─┘  ╚═╝ ┴ ┴ ┴ ┴ └─┘└─┘

╭──────────────┬─────────┬─────────╮
│ Container    │ Status  │ Uptime  │
├──────────────┼─────────┼─────────┤
│ homelab_adg  │ Running │ 3d 5h   │
│ homelab_vpn  │ Running │ 3d 5h   │
│ homelab_graf │ Stopped │ N/A     │
╰──────────────┴─────────┴─────────╯
```

### Phase 1 Summary

**What We Built:**
- ✅ Project structure
- ✅ `homelab status` command
- ✅ Docker integration
- ✅ Dependency injection
- ✅ Clean architecture

**What We Learned:**
- ✅ Spectre.Console basics (tables, colors)
- ✅ Command pattern
- ✅ Interfaces for testability
- ✅ Docker.DotNet SDK
- ✅ Async/await

**Lines of Code:** ~200 (manageable!)

---

## Phase 2: Service Lifecycle Control

### Goals

- ✅ Implement `homelab service start <name>`
- ✅ Implement `homelab service stop <name>`
- ✅ Implement `homelab service restart <name>`
- ✅ Add health checks after operations
- ✅ Handle errors gracefully

### Implementation Guide

#### Step 2.1: Create ServiceCommand

**File:** `src/HomelabCli/Commands/ServiceCommand.cs`

```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using HomelabCli.Services.Docker;
using System.ComponentModel;

namespace HomelabCli.Commands;

/// <summary>
/// Handles service lifecycle operations (start, stop, restart).
/// Usage: homelab service start adguard
/// </summary>
public class ServiceCommand : AsyncCommand<ServiceCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action to perform: start, stop, restart")]
        public string Action { get; set; } = string.Empty;
        
        [CommandArgument(1, "<service>")]
        [Description("Service name (e.g., adguard, wireguard)")]
        public string ServiceName { get; set; } = string.Empty;
    }
    
    private readonly IDockerService _dockerService;
    
    public ServiceCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }
    
    public override async Task<int> ExecuteAsync(
        CommandContext context, 
        Settings settings)
    {
        // Validate action
        var validActions = new[] { "start", "stop", "restart" };
        if (!validActions.Contains(settings.Action.ToLower()))
        {
            AnsiConsole.MarkupLine(
                "[red]Invalid action.[/] Valid: start, stop, restart");
            return 1; // Error exit code
        }
        
        // Perform action
        try
        {
            await AnsiConsole.Status()
                .StartAsync($"{settings.Action}ing {settings.ServiceName}...", 
                async ctx =>
            {
                switch (settings.Action.ToLower())
                {
                    case "start":
                        await _dockerService.StartContainerAsync(
                            settings.ServiceName);
                        break;
                    case "stop":
                        await _dockerService.StopContainerAsync(
                            settings.ServiceName);
                        break;
                    case "restart":
                        await _dockerService.StopContainerAsync(
                            settings.ServiceName);
                        await Task.Delay(2000); // Wait 2s
                        await _dockerService.StartContainerAsync(
                            settings.ServiceName);
                        break;
                }
            });
            
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Successfully {settings.Action}ed {settings.ServiceName}");
            
            return 0; // Success
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1; // Error
        }
    }
}
```

**New Concepts:**

1. **`CommandSettings`**:
    - Spectre parses arguments into this class
    - `[CommandArgument(0, "<action>")]` - first arg
    - Automatic validation, help text generation

2. **`AnsiConsole.Status()`**:
    - Shows a spinner during long operations
    - Keeps user informed

3. **Error Handling**:
    - `try/catch` prevents crashes
    - Return non-zero exit code on error
    - Unix convention for scripting

#### Step 2.2: Add Command to Program.cs

```csharp
app.Configure(config =>
{
    config.AddCommand<StatusCommand>("status");
    config.AddCommand<ServiceCommand>("service")
        .WithDescription("Manage service lifecycle");
});
```

#### Step 2.3: Test Service Control

```bash
# Start a service
dotnet run -- service start adguard

# Stop a service
dotnet run -- service stop adguard

# Restart a service
dotnet run -- service restart adguard
```

**Expected Output:**
```
⠋ Starting adguard...
✓ Successfully started adguard
```

### Phase 2 Summary

**What We Added:**
- ✅ Service lifecycle management
- ✅ Command arguments parsing
- ✅ Progress indicators
- ✅ Error handling

**Key Learnings:**
- `CommandSettings` for arguments
- `AnsiConsole.Status()` for spinners
- Proper error codes

---

## Phase 3: Configuration Management

### Goals

- ✅ View current configurations
- ✅ Edit configurations interactively
- ✅ Backup before changes
- ✅ Validate configurations

### Implementation

*(Continue pattern: Command → Service → Model)*

#### Step 3.1: Create ConfigCommand

**File:** `src/HomelabCli/Commands/ConfigCommand.cs`

```csharp
// Similar structure to ServiceCommand
// Shows docker-compose.yml content
// Allows editing via $EDITOR
```

#### Step 3.2: Create IConfigService

```csharp
public interface IConfigService
{
    Task<string> GetComposeFileAsync();
    Task UpdateComposeFileAsync(string content);
    Task BackupConfigAsync();
}
```

---

## Phase 4: Maintenance & Automation

### Goals

- ✅ Backup/restore functionality
- ✅ Update management
- ✅ Log viewing
- ✅ Cleanup utilities

### Implementation

*(Similar pattern continues)*

---

## Testing Strategy

### Unit Tests

```csharp
// Example test for StatusCommand
[Fact]
public async Task StatusCommand_ShowsRunningContainers()
{
    // Arrange
    var mockDocker = new MockDockerService();
    mockDocker.SetupContainers(new List<ContainerInfo> 
    {
        new() { Name = "test", IsRunning = true }
    });
    
    var command = new StatusCommand(mockDocker);
    
    // Act
    var result = await command.ExecuteAsync(null);
    
    // Assert
    Assert.Equal(0, result); // Success exit code
}
```

**Why Test?**

1. **Confidence**: Know code works
2. **Refactoring**: Change internals safely
3. **Documentation**: Tests show usage examples

---

## Deployment & Distribution

### Building Release Binary

```bash
# Build self-contained binary (no .NET needed on target)
dotnet publish -c Release -r osx-arm64 --self-contained

# Creates: bin/Release/net8.0/osx-arm64/publish/HomelabCli
```

### Installation

```bash
# Copy to PATH
sudo cp HomelabCli /usr/local/bin/homelab
sudo chmod +x /usr/local/bin/homelab

# Now works globally
homelab status
```

### GitHub Release

```yaml
# .github/workflows/release.yml
name: Release
on:
  push:
    tags:
      - 'v*'
jobs:
  build:
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      - run: dotnet publish -c Release -r osx-arm64 --self-contained
      - uses: softprops/action-gh-release@v1
        with:
          files: bin/Release/net8.0/osx-arm64/publish/HomelabCli
```

---

## Next Steps

After completing all 4 phases:

1. **Add Services**: VPN, DNS, Monitoring
2. **Remote Management**: SSH to Mac Mini
3. **Advanced Features**:
    - Service dependencies
    - Health monitoring
    - Alerting integration
4. **Polish**:
    - Configuration wizard
    - Better error messages
    - Auto-completion scripts

---

## Reference Links

- **Spectre.Console Docs**: https://spectreconsole.net/
- **Docker.DotNet**: https://github.com/dotnet/Docker.DotNet
- **.NET CLI**: https://learn.microsoft.com/en-us/dotnet/core/tools/

---

## Glossary

- **CLI**: Command Line Interface
- **DI**: Dependency Injection
- **DTO**: Data Transfer Object (model with no logic)
- **SOLID**: Software design principles (Single Responsibility, etc.)
- **KISS**: Keep It Simple, Stupid
- **DRY**: Don't Repeat Yourself
- **Async**: Non-blocking operations (network I/O)
- **LINQ**: Language Integrated Query (functional operations on collections)

---

**End of Implementation Guide v1.0**