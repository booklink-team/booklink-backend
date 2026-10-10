using Booklink.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Booklink.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddSingleton(TimeProvider.System)
        .AddScoped<AuthService>()
        .AddScoped<CatalogService>()
        .AddScoped<ReservationService>()
        .AddScoped<LoanService>()
        .AddScoped<ReadingListService>();
}
