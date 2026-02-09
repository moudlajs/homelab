# Homelab CLI - AI Development Guide

## Project Overview
**HomeLab CLI** - Production-ready C# command-line tool for managing Mac Mini M4 homelab infrastructure.

**Tech Stack:**
- .NET 8.0 (C#, target: `osx-arm64`)
- Spectre.Console (UI framework + command routing)
- Docker.DotNet (container management)
- SSH.NET (remote operations)
- xUnit + Moq + FluentAssertions (testing)

**Version:** See `HomeLab.Cli.csproj` and `CHANGELOG.md` for current version

---

## Non-Negotiable Rules

### 1. PR Workflow (ENFORCED by branch protection)
- **NEVER commit directly to `main`**
- Create feature branch: `feature/name`, `fix/name`, `chore/name`
- All changes go through PR with CI validation
- Branch protection requires: CI passing, code formatting

### 2. Code Quality Gates
```bash
dotnet test                          # Must pass before commit
dotnet format --verify-no-changes    # Enforced in CI
/code-review                         # Use before pushing PR
```

### 3. Commit Convention (Conventional Commits)
```
feat: add new feature
fix: bug fix
docs: documentation only
chore: maintenance (deps, cleanup)
test: add/update tests
refactor: code restructuring
```

### 4. Testing Requirements
- All services MUST have interfaces (`I<ServiceName>`)
- New features MUST include unit tests
- Mock external dependencies (Docker, HTTP, SSH)
- Use FluentAssertions for readable assertions

---

## Architecture Patterns

### Layer Structure
```
Commands/           UI layer - Spectre.Console commands
├── Dns/           AdGuard Home
├── HomeAssistant/ Home Assistant
├── Monitor/       AI monitoring (Claude)
├── Network/       nmap, ntopng, Suricata
├── Remote/        SSH management
├── Speedtest/     Speed testing
├── Traefik/       Reverse proxy
├── Tv/            LG WebOS control
├── Uptime/        Uptime Kuma
├── Vpn/           Tailscale VPN
└── ...

Services/           Business logic - all have interfaces
├── Abstractions/  Interface definitions
├── Docker/        DockerService (container management)
├── Configuration/ Config file handling
├── Health/        Health checks
├── Tailscale/     VPN client (CLI wrapper)
├── AI/            Claude API integration
└── ...

Models/            Data structures (DTOs, config models)
```

### Service Pattern
```csharp
// 1. Define interface in Services/Abstractions/
public interface IMyService
{
    Task<Result> DoSomethingAsync();
}

// 2. Implement in Services/MyDomain/
public class MyService : IMyService
{
    // Constructor injection of dependencies
    public MyService(IDependency dep) { }
}

// 3. Register in Program.cs
services.AddSingleton<IMyService, MyService>();

// 4. Inject into commands
public class MyCommand : Command<Settings>
{
    private readonly IMyService _service;

    public MyCommand(IMyService service)
    {
        _service = service;
    }
}
```

### Command Pattern (Spectre.Console.Cli)
```csharp
public class MyCommand : Command<MyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<arg>")]
        [Description("Argument description")]
        public string Arg { get; set; } = string.Empty;

        [CommandOption("--flag")]
        [Description("Optional flag")]
        public bool Flag { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Business logic via injected services
        return 0; // Success
    }
}
```

---

## Key Dependencies & Their Use

| Package | Purpose | Where Used |
|---------|---------|------------|
| `Spectre.Console` | Rich terminal UI, tables, progress | All commands for output |
| `Spectre.Console.Cli` | Command routing, argument parsing | Program.cs, all commands |
| `Docker.DotNet` | Docker API client | DockerService |
| `SSH.NET` | Remote SSH operations | Remote commands |
| `YamlDotNet` | Config file parsing | HomelabConfigService |
| `CsvHelper` | CSV export functionality | OutputFormatter |
| `ReadLine` | Interactive shell input | ShellCommand |

---

## Common Patterns

### Output Formatting
```csharp
// Use OutputFormatter for consistent table output
var formatter = new OutputFormatter();
formatter.RenderTable(data, columns, title);

// Support --output flag for CSV/JSON export
if (settings.Output == "csv") {
    formatter.ExportToCsv(data, "output.csv");
}
```

### Error Handling
```csharp
// Use Spectre.Console for user-friendly errors
if (error) {
    AnsiConsole.MarkupLine("[red]Error: Clear message here[/]");
    return 1; // Non-zero exit code
}

// Wrap external calls in try-catch
try {
    await _dockerService.SomeOperationAsync();
}
catch (Exception ex) {
    AnsiConsole.MarkupLine($"[red]Operation failed: {ex.Message}[/]");
    return 1;
}
```

### Async Operations
```csharp
// Use async/await for all I/O operations
public async Task<List<Container>> GetContainersAsync()
{
    return await _dockerClient.Containers.ListContainersAsync(...);
}

// CancellationToken support (from Execute signature)
await operation(cancellationToken);
```

---

## Testing Guidelines

### Unit Test Structure
```csharp
public class MyServiceTests
{
    private readonly Mock<IDependency> _mockDep;
    private readonly MyService _sut; // System Under Test

    public MyServiceTests()
    {
        _mockDep = new Mock<IDependency>();
        _sut = new MyService(_mockDep.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        _mockDep.Setup(x => x.Method()).ReturnsAsync(value);

        // Act
        var result = await _sut.MethodUnderTest();

        // Assert
        result.Should().NotBeNull();
        result.Property.Should().Be(expected);
    }
}
```

### What to Test
- ✅ Business logic in services
- ✅ Edge cases and error handling
- ✅ Data transformations
- ❌ Don't test Spectre.Console UI rendering
- ❌ Don't test external APIs (mock them)

---

## Common Pitfalls

### ❌ DON'T
```csharp
// Direct Docker API calls in commands
var containers = await dockerClient.Containers.ListContainersAsync();

// Hardcoded paths
var config = File.ReadAllText("/Users/me/.homelab/config.yaml");

// Console.WriteLine for output
Console.WriteLine("Status: Running");
```

### ✅ DO
```csharp
// Use services
var containers = await _dockerService.GetContainersAsync();

// Use config service for paths
var config = await _configService.LoadConfigAsync();

// Use Spectre.Console
AnsiConsole.MarkupLine("[green]Status:[/] Running");
```

---

## File Organization

### Where Things Go
- **New command**: `Commands/<Domain>/<CommandName>Command.cs`
- **New service**: `Services/<Domain>/<ServiceName>.cs`
- **Service interface**: `Services/Abstractions/I<ServiceName>.cs`
- **Data models**: `Models/<Domain>/<ModelName>.cs`
- **Tests**: Mirror source structure in test project

### Config Files
- `config/homelab-cli.yaml` - User configuration
- `data/` - Persistent service data (gitignored)

---

## Development Workflow

### Adding a New Command
1. Create command class in `Commands/<Domain>/`
2. Implement `Command<Settings>` with proper settings class
3. Register in `Program.cs` using `config.AddCommand<T>()` or `config.AddBranch()`
4. Add to completion scripts in `CompletionCommand.cs`
5. Update CHANGELOG.md
6. Write tests
7. Create PR

### Adding a New Service
1. Define interface in `Services/Abstractions/I<Name>.cs`
2. Implement in `Services/<Domain>/<Name>.cs`
3. Register in `Program.cs` DI container
4. Inject into commands that need it
5. Write unit tests with mocks
6. Create PR

### Release Process
1. Update version in `HomeLab.Cli.csproj`
2. Update CHANGELOG.md with new version entry
3. Create PR: `chore: bump version to X.Y.Z`
4. Merge PR (CI builds and tests)
5. Create GitHub release with tag `vX.Y.Z`
6. Upload built binary (from CI artifacts or local build)

---

## Troubleshooting

### Build Issues
```bash
dotnet restore              # Restore packages
dotnet clean               # Clean build artifacts
dotnet build              # Build project
```

### Test Failures
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Format Issues
```bash
dotnet format             # Auto-fix formatting
```

### CI Failures
- Check GitHub Actions logs
- Run same commands locally: `dotnet test`, `dotnet format --verify-no-changes`
- Ensure all using statements are ordered (System namespaces first)

---

## References

- **CHANGELOG.md** - Version history and feature timeline
- **README.md** - User-facing documentation
- **docs/** - Implementation guides and architecture docs
- **.github/workflows/** - CI/CD pipeline configuration