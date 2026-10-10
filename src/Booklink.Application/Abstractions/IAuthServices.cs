using Booklink.Domain.Entities;

namespace Booklink.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Create(User user);
}
