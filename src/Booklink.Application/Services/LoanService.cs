using Booklink.Application.Abstractions;
using Booklink.Domain;
using Booklink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Services;

public class LoanService(IAppDbContext db, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<LoanDto>> GetMineAsync(int userId, CancellationToken ct)
    {
        var loans = await WithDetails().Where(l => l.UserId == userId)
            .OrderBy(l => l.Status).ThenByDescending(l => l.IssuedAt).ToListAsync(ct);
        var now = Now;
        var hasOverdue = loans.Any(l => l.IsOverdue(now));
        return loans.Select(l => ToDto(l, now, hasOverdue, null)).ToList();
    }

    public Task<LoanDto> RenewAsync(int userId, int id, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var now = Now;
            var loan = await WithDetails().SingleOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct)
                       ?? throw new NotFoundException("Выдача не найдена.");
            var hasOverdue = await HasOverdueAsync(userId, now, ct);
            loan.Renew(now, hasOverdue);
            await db.SaveChangesAsync(ct);
            return ToDto(loan, now, hasOverdue, null);
        }, ct);

    // --- Библиотекарь ---

    public Task<LoanDto> IssueAsync(int reservationId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var now = Now;
            var r = await db.Reservations.Include(r => r.Copy).ThenInclude(c => c.Book)
                        .SingleOrDefaultAsync(r => r.Id == reservationId, ct)
                    ?? throw new NotFoundException("Бронирование не найдено.");

            // Повторное подтверждение выдачи возвращает уже созданную выдачу.
            if (r.Status == ReservationStatus.Completed)
                return await GetAsync(await db.Loans.Where(l => l.ReservationId == r.Id).Select(l => l.Id).SingleAsync(ct), ct);

            if (r.Status == ReservationStatus.Active
                && await db.Loans.CountAsync(l => l.UserId == r.UserId && l.Status == LoanStatus.Active, ct) >= LibraryRules.MaxActiveLoans)
                throw new DomainException("loan_limit", $"У читателя уже {LibraryRules.MaxActiveLoans} действующих выдач.");

            var loan = r.Issue(now);
            db.Loans.Add(loan);
            await db.SaveChangesAsync(ct);
            return await GetAsync(loan.Id, ct);
        }, ct);

    public Task<LoanDto> ReturnAsync(int id, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var loan = await db.Loans.Include(l => l.Copy).SingleOrDefaultAsync(l => l.Id == id, ct)
                       ?? throw new NotFoundException("Выдача не найдена.");
            loan.Return(Now);
            await db.SaveChangesAsync(ct);
            return await GetAsync(id, ct);
        }, ct);

    public async Task<IReadOnlyList<LoanDto>> SearchActiveAsync(string? search, CancellationToken ct)
    {
        var query = WithDetails().Include(l => l.User).Where(l => l.Status == LoanStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.User.Name.ToLower().Contains(s) || l.User.Login.ToLower().Contains(s)
                                     || l.Copy.Book.Title.ToLower().Contains(s) || l.Copy.InventoryNumber.ToLower().Contains(s));
        }
        var loans = await query.OrderBy(l => l.DueAt).ToListAsync(ct);
        var now = Now;
        var overdueUsers = loans.Where(l => l.IsOverdue(now)).Select(l => l.UserId).ToHashSet();
        return loans.Select(l => ToDto(l, now, overdueUsers.Contains(l.UserId), l.User)).ToList();
    }

    public async Task<LoanDto> GetAsync(int id, CancellationToken ct)
    {
        var loan = await WithDetails().Include(l => l.User).SingleOrDefaultAsync(l => l.Id == id, ct)
                   ?? throw new NotFoundException("Выдача не найдена.");
        var now = Now;
        return ToDto(loan, now, await HasOverdueAsync(loan.UserId, now, ct), loan.User);
    }

    private Task<bool> HasOverdueAsync(int userId, DateTime now, CancellationToken ct) =>
        db.Loans.AnyAsync(l => l.UserId == userId && l.Status == LoanStatus.Active && l.DueAt < now, ct);

    private IQueryable<Loan> WithDetails() => db.Loans.Include(l => l.Copy).ThenInclude(c => c.Book);

    private static LoanDto ToDto(Loan l, DateTime now, bool userHasOverdue, User? reader)
    {
        var reason = l.RenewalDenyReason(now, userHasOverdue);
        return new LoanDto(
            l.Id, new BookShortDto(l.Copy.Book.Id, l.Copy.Book.Title, l.Copy.Book.Authors, l.Copy.Book.CoverUrl),
            l.Copy.InventoryNumber, l.IssuedAt, l.DueAt, l.ReturnedAt, l.RenewalCount, l.Status,
            l.IsOverdue(now), reason is null, reason, reason is null ? l.DueAt + LibraryRules.RenewalTerm : null,
            reader is null ? null : new ReaderDto(reader.Id, reader.Name, reader.Login));
    }
}
