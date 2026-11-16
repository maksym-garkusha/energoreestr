namespace Energoreestr.Import;

/// <summary>
/// Перевіряє портал за розкладом, поки працює веб-сервер. Вмикається налаштуванням
/// Import:IntervalHours; якщо там 0, реєстри оновлюються тільки командою import.
/// </summary>
public class ImportWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ImportWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        double hours = config.GetValue<double>("Import:IntervalHours");
        if (hours <= 0) return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        do
        {
            try
            {
                // Importer працює з базою, а контекст бази живе в межах scope
                using var scope = scopes.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<Importer>();
                await importer.ImportAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Плановий імпорт не вдався");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
