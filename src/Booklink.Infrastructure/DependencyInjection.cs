using System.Text;
using Booklink.Application.Abstractions;
using Booklink.Infrastructure.Auth;
using Booklink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Booklink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default")
                         ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default.");
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection, npg => npg.EnableRetryOnFailure()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Не задана секция Jwt.");
        if (Encoding.UTF8.GetByteCount(jwt.Key ?? "") < 32)
            throw new InvalidOperationException("Jwt:Key должен быть не короче 32 байт.");
        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHostedService<ReservationExpiryWorker>();
        return services;
    }
}
