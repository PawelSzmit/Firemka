using Firemka.Infrastructure.Jobs;

namespace Firemka.Worker;

public class Worker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var nextSalesScheduleCheckUtc = DateTimeOffset.MinValue;
        var nextNotificationCheckUtc = DateTimeOffset.MinValue;
        logger.LogInformation("Durable worker {WorkerId} started.", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var runner = scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>();
                var nowUtc = DateTimeOffset.UtcNow;
                if (nowUtc >= nextSalesScheduleCheckUtc)
                {
                    var salesScheduler = scope.ServiceProvider.GetRequiredService<Firemka.Infrastructure.Sales.SalesDraftScheduler>();
                    await salesScheduler.EnqueueDueAsync(nowUtc, stoppingToken);
                    nextSalesScheduleCheckUtc = nowUtc.AddMinutes(1);
                }
                if (nowUtc >= nextNotificationCheckUtc)
                {
                    var scheduler = scope.ServiceProvider.GetRequiredService<Firemka.Application.Notifications.INotificationScheduler>();
                    await scheduler.EnqueueDueAsync(nowUtc, stoppingToken);
                    nextNotificationCheckUtc = nowUtc.AddMinutes(1);
                }

                var dispatched = await dispatcher.DispatchNextAsync(nowUtc, stoppingToken);
                var executed = await runner.RunNextAsync(workerId, nowUtc, stoppingToken);
                if (!dispatched && !executed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Durable worker loop failed; retrying after a delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
