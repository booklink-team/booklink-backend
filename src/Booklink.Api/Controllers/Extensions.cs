using System.Security.Claims;

namespace Booklink.Api.Controllers;

public static class Roles
{
    public const string Student = nameof(Domain.UserRole.Student);
    public const string Librarian = nameof(Domain.UserRole.Librarian);
}

public static class ClaimsExtensions
{
    public static int Id(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);
}
