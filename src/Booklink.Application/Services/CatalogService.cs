using Booklink.Application.Abstractions;
using Booklink.Domain;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Services;

public class CatalogService(IAppDbContext db)
{
    public async Task<PagedResult<BookListItemDto>> SearchAsync(string? search, int page, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100_000); // без переполнения (page - 1) * PageSize
        var query = db.Books.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(s) || b.Authors.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Title).ThenBy(b => b.Id)
            .Skip((page - 1) * LibraryRules.PageSize).Take(LibraryRules.PageSize)
            .Select(b => new BookListItemDto(b.Id, b.Title, b.Authors, b.Year, b.CoverUrl,
                b.Copies.Count(c => c.State == CopyState.Available)))
            .ToListAsync(ct);
        return new PagedResult<BookListItemDto>(items, page, LibraryRules.PageSize, total);
    }

    public async Task<BookDto> GetAsync(int id, CancellationToken ct) =>
        await db.Books.Where(b => b.Id == id)
            .Select(b => new BookDto(b.Id, b.Title, b.Authors, b.Description, b.Year, b.CoverUrl,
                b.Copies.Count(c => c.State == CopyState.Available), b.Copies.Count,
                b.ReservationForbidden, b.RenewalForbidden))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Книга не найдена.");
}
