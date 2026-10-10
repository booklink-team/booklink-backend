using Booklink.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Booklink.Infrastructure;

/// <summary>Автоматическое истечение броней (ТЗ: не реже раза в минуту).</summary>
public class ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<ReservationService>().ExpireAsync(ct);
                if (n > 0) log.LogInformation("Истекло бронирований: {Count}", n);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Ошибка обработки истёкших бронирований");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
