using Booklink.Application.Abstractions;
using Booklink.Domain;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Services;

public class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens)
{
    public async Task<LoginResult> LoginAsync(string login, string password, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login.Trim(), ct);
        if (user is null || !hasher.Verify(user.PasswordHash, password))
            throw new DomainException("invalid_credentials", "Неверный логин или пароль.");

        var (token, expiresAt) = tokens.Create(user);
        return new LoginResult(token, expiresAt, new UserDto(user.Id, user.Name, user.Login, user.Role));
    }

    public async Task<UserDto> GetAsync(int userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Name, u.Login, u.Role))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Пользователь не найден.");
}
