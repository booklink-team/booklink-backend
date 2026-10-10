namespace Booklink.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Login { get; set; }
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = "";
}
