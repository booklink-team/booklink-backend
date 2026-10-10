using Booklink.Application.Abstractions;
using Booklink.Domain;
using Booklink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Services;

public class ReservationService(IAppDbContext db, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<ReservationDto> CreateAsync(int userId, int bookId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var now = Now;
            await ExpireStaleAsync(now, ct);

            var book = await db.Books.FindAsync([bookId], ct) ?? throw new NotFoundException("Книга не найдена.");
            if (book.ReservationForbidden)
                throw new DomainException("reservation_forbidden", "Бронирование этой книги запрещено.");

            var live = db.Reservations.Where(r => r.UserId == userId && r.Status == ReservationStatus.Active);
            if (await live.AnyAsync(r => r.Copy.BookId == bookId, ct))
                throw new DomainException("already_reserved", "У вас уже есть активная бронь этой книги.");
            if (await live.CountAsync(ct) >= LibraryRules.MaxActiveReservations)
                throw new DomainException("reservation_limit", $"Нельзя иметь больше {LibraryRules.MaxActiveReservations} активных бронирований.");

            var loans = db.Loans.Where(l => l.UserId == userId && l.Status == LoanStatus.Active);
            if (await loans.AnyAsync(l => l.Copy.BookId == bookId, ct))
                throw new DomainException("already_issued", "Эта книга уже выдана вам.");
            if (await loans.AnyAsync(l => l.DueAt < now, ct))
                throw new DomainException("has_overdue", "Бронирование недоступно: есть просроченная выдача.");
            if (await loans.CountAsync(ct) >= LibraryRules.MaxActiveLoans)
                throw new DomainException("loan_limit", $"Нельзя иметь больше {LibraryRules.MaxActiveLoans} действующих выдач.");

            var copy = await db.Copies.Include(c => c.Book)
                .Where(c => c.BookId == bookId && c.State == CopyState.Available)
                .OrderBy(c => c.Id).FirstOrDefaultAsync(ct)
                ?? throw new DomainException("no_available_copies", "Нет доступных экземпляров.");

            copy.State = CopyState.Reserved;
            var reservation = new Reservation
            {
                UserId = userId, Copy = copy, CreatedAt = now,
                ExpiresAt = now + LibraryRules.ReservationTerm, Status = ReservationStatus.Active,
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            return ToDto(reservation, null);
        }, ct);

    public async Task<IReadOnlyList<ReservationDto>> GetMineAsync(int userId, CancellationToken ct)
    {
        await ExpireAsync(ct);
        var list = await WithDetails().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return list.Select(r => ToDto(r, null)).ToList();
    }

    public Task<ReservationDto> CancelAsync(int userId, int id, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var r = await WithDetails().SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct)
                    ?? throw new NotFoundException("Бронирование не найдено.");
            r.Cancel(Now);
            await db.SaveChangesAsync(ct);
            return ToDto(r, null);
        }, ct);

    // --- Библиотекарь ---

    public async Task<IReadOnlyList<ReservationDto>> SearchActiveAsync(string? search, CancellationToken ct)
    {
        await ExpireAsync(ct);
        var query = WithDetails().Include(r => r.User).Where(r => r.Status == ReservationStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            var id = int.TryParse(s, out var n) ? n : -1;
            query = query.Where(r => r.Id == id || r.User.Name.ToLower().Contains(s) || r.User.Login.ToLower().Contains(s));
        }
        var list = await query.OrderBy(r => r.ExpiresAt).ToListAsync(ct);
        return list.Select(r => ToDto(r, r.User)).ToList();
    }

    public async Task<ReservationDto> GetAsync(int id, CancellationToken ct)
    {
        await ExpireAsync(ct);
        var r = await WithDetails().Include(r => r.User).SingleOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("Бронирование не найдено.");
        return ToDto(r, r.User);
    }

    /// <summary>Переводит просроченные активные брони в «истекло» и освобождает экземпляры.</summary>
    public Task<int> ExpireAsync(CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var n = await ExpireStaleAsync(Now, ct);
            if (n > 0) await db.SaveChangesAsync(ct);
            return n;
        }, ct);

    private async Task<int> ExpireStaleAsync(DateTime now, CancellationToken ct)
    {
        var stale = await db.Reservations.Include(r => r.Copy)
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now).ToListAsync(ct);
        foreach (var r in stale)
        {
            r.Status = ReservationStatus.Expired;
            r.Copy.State = CopyState.Available;
        }
        return stale.Count;
    }

    private IQueryable<Reservation> WithDetails() => db.Reservations.Include(r => r.Copy).ThenInclude(c => c.Book);

    internal static ReservationDto ToDto(Reservation r, User? reader) => new(
        r.Id, new BookShortDto(r.Copy.Book.Id, r.Copy.Book.Title, r.Copy.Book.Authors, r.Copy.Book.CoverUrl),
        r.Copy.InventoryNumber, r.CreatedAt, r.ExpiresAt, r.Status,
        reader is null ? null : new ReaderDto(reader.Id, reader.Name, reader.Login));
}
