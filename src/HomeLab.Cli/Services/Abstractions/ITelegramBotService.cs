namespace HomeLab.Cli.Services.Abstractions;

public interface ITelegramBotService
{
    Task StartAsync(CancellationToken cancellationToken);
    bool IsConfigured();
}
