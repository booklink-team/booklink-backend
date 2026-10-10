using Booklink.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Booklink.Infrastructure.Auth;

/// <summary>PBKDF2 из ASP.NET Core Identity.</summary>
public class PasswordHasher : Application.Abstractions.IPasswordHasher
{
    private static readonly PasswordHasher<User> Inner = new();
    private static readonly User Dummy = new() { Name = "", Login = "" };

    public string Hash(string password) => Inner.HashPassword(Dummy, password);

    public bool Verify(string hash, string password) =>
        Inner.VerifyHashedPassword(Dummy, hash, password) != PasswordVerificationResult.Failed;
}
