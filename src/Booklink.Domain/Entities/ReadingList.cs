namespace Booklink.Domain.Entities;

public class ReadingList
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Name { get; private set; } = "";
    /// <summary>Для уникальности без учёта регистра.</summary>
    public string NormalizedName { get; private set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<ReadingListItem> Items { get; set; } = [];

    public void Rename(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length is 0 or > LibraryRules.ListNameMaxLength)
            throw new DomainException("invalid_list_name", "Название списка должно содержать от 1 до 50 символов.");
        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
    }
}

public class ReadingListItem
{
    public int ReadingListId { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateTime AddedAt { get; set; }
}
