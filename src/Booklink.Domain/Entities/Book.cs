namespace Booklink.Domain.Entities;

public class Book
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Authors { get; set; }
    public string? Description { get; set; }
    public int? Year { get; set; }
    public string? CoverUrl { get; set; }
    public bool ReservationForbidden { get; set; }
    public bool RenewalForbidden { get; set; }
    public List<Copy> Copies { get; set; } = [];
}

public class Copy
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public required string InventoryNumber { get; set; }
    public CopyState State { get; set; }
}
