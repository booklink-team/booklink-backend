using Booklink.Application.Abstractions;
using Booklink.Domain;
using Booklink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Services;

public class ReadingListService(IAppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<ReadingListDto>> GetMineAsync(int userId, CancellationToken ct) =>
        await db.ReadingLists.Where(l => l.OwnerId == userId).OrderBy(l => l.Name)
            .Select(l => new ReadingListDto(l.Id, l.Name, l.Items.Count, l.CreatedAt))
            .ToListAsync(ct);

    public async Task<ReadingListDetailsDto> GetAsync(int userId, int id, CancellationToken ct)
    {
        var list = await Find(userId, id, ct);
        var books = await db.ReadingListItems.Where(i => i.ReadingListId == id).OrderBy(i => i.AddedAt)
            .Select(i => new BookListItemDto(i.Book.Id, i.Book.Title, i.Book.Authors, i.Book.Year, i.Book.CoverUrl,
                i.Book.Copies.Count(c => c.State == CopyState.Available)))
            .ToListAsync(ct);
        return new ReadingListDetailsDto(list.Id, list.Name, list.CreatedAt, books);
    }

    public async Task<ReadingListDto> CreateAsync(int userId, string name, CancellationToken ct)
    {
        var list = new ReadingList { OwnerId = userId, CreatedAt = clock.GetUtcNow().UtcDateTime };
        list.Rename(name);
        await EnsureUniqueAsync(userId, list, ct);
        db.ReadingLists.Add(list);
        await db.SaveChangesAsync(ct);
        return new ReadingListDto(list.Id, list.Name, 0, list.CreatedAt);
    }

    public async Task<ReadingListDto> RenameAsync(int userId, int id, string name, CancellationToken ct)
    {
        var list = await Find(userId, id, ct);
        list.Rename(name);
        await EnsureUniqueAsync(userId, list, ct);
        await db.SaveChangesAsync(ct);
        return new ReadingListDto(list.Id, list.Name, await db.ReadingListItems.CountAsync(i => i.ReadingListId == id, ct), list.CreatedAt);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken ct)
    {
        db.ReadingLists.Remove(await Find(userId, id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task AddBookAsync(int userId, int id, int bookId, CancellationToken ct)
    {
        await Find(userId, id, ct);
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct)) throw new NotFoundException("Книга не найдена.");
        if (await db.ReadingListItems.AnyAsync(i => i.ReadingListId == id && i.BookId == bookId, ct))
            throw new DomainException("book_already_in_list", "Книга уже есть в этом списке.");
        db.ReadingListItems.Add(new ReadingListItem { ReadingListId = id, BookId = bookId, AddedAt = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveBookAsync(int userId, int id, int bookId, CancellationToken ct)
    {
        await Find(userId, id, ct);
        var item = await db.ReadingListItems.FindAsync([id, bookId], ct)
                   ?? throw new NotFoundException("Книги нет в списке.");
        db.ReadingListItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ReadingList> Find(int userId, int id, CancellationToken ct) =>
        await db.ReadingLists.SingleOrDefaultAsync(l => l.Id == id && l.OwnerId == userId, ct)
        ?? throw new NotFoundException("Список не найден.");

    private async Task EnsureUniqueAsync(int userId, ReadingList list, CancellationToken ct)
    {
        if (await db.ReadingLists.AnyAsync(l => l.OwnerId == userId && l.Id != list.Id && l.NormalizedName == list.NormalizedName, ct))
            throw new DomainException("list_name_taken", "Список с таким названием уже существует.");
    }
}
