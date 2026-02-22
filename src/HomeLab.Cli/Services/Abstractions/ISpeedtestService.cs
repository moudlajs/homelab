using HomeLab.Cli.Models.EventLog;

namespace HomeLab.Cli.Services.Abstractions;

public interface ISpeedtestService
{
    Task<SpeedtestSnapshot> RunAsync();
    bool IsInstalled();
}
