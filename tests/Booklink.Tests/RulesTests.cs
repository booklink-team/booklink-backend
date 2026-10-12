using Booklink.Application.Services;
using Booklink.Domain;
using Booklink.Domain.Entities;
using Booklink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Booklink.Tests;

public class RulesTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private class FixedClock(DateTime now) : TimeProvider
    {
        public DateTime Value { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Value;
    }

    private static (AppDbContext Db, FixedClock Clock) Setup(int books = 5, int copies = 1)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Users.Add(new User { Id = 1, Name = "Студент", Login = "s", Role = UserRole.Student });
        var inv = 0;
        for (var i = 1; i <= books; i++)
            db.Books.Add(new Book
            {
                Id = i, Title = $"Книга {i}", Authors = "Автор",
                Copies = Enumerable.Range(0, copies).Select(_ => new Copy { InventoryNumber = $"I{inv++}" }).ToList(),
            });
        db.SaveChanges();
        return (db, new FixedClock(Now));
    }

    [Fact]
    public async Task Reservation_limits_and_lifecycle()
    {
        var (db, clock) = Setup();
        var reservations = new ReservationService(db, clock);
        var loans = new LoanService(db, clock);

        var r1 = await reservations.CreateAsync(1, 1, default);
        Assert.Equal(Now.AddHours(48), r1.ExpiresAt);
        Assert.Equal("already_reserved", (await Assert.ThrowsAsync<DomainException>(() => reservations.CreateAsync(1, 1, default))).Code);

        await reservations.CreateAsync(1, 2, default);
        await reservations.CreateAsync(1, 3, default);
        Assert.Equal("reservation_limit", (await Assert.ThrowsAsync<DomainException>(() => reservations.CreateAsync(1, 4, default))).Code);

        // Выдача: бронь завершается, повторное подтверждение — та же выдача.
        var loan = await loans.IssueAsync(r1.Id, default);
        Assert.Equal(loan.Id, (await loans.IssueAsync(r1.Id, default)).Id);
        Assert.Equal(Now.AddDays(14), loan.DueAt);
        Assert.Equal(CopyState.Issued, db.Copies.Single(c => c.BookId == 1).State);
        Assert.Equal("already_issued", (await Assert.ThrowsAsync<DomainException>(() => reservations.CreateAsync(1, 1, default))).Code);

        // Продление: +7 дней один раз, повтор не продлевает.
        Assert.Equal(Now.AddDays(21), (await loans.RenewAsync(1, loan.Id, default)).DueAt);
        await Assert.ThrowsAsync<DomainException>(() => loans.RenewAsync(1, loan.Id, default));
        Assert.Equal(Now.AddDays(21), db.Loans.Single().DueAt);

        // Истечение брони освобождает экземпляр; просроченную бронь нельзя выдать.
        clock.Value = Now.AddHours(49);
        var r2 = db.Reservations.Single(r => r.Copy.BookId == 2);
        Assert.Equal(2, await reservations.ExpireAsync(default));
        Assert.Equal(ReservationStatus.Expired, r2.Status);
        Assert.Equal(CopyState.Available, db.Copies.Single(c => c.BookId == 2).State);
        await Assert.ThrowsAsync<DomainException>(() => loans.IssueAsync(r2.Id, default));

        // Просрочка запрещает бронирование.
        clock.Value = Now.AddDays(22);
        Assert.True((await loans.GetMineAsync(1, default)).Single().IsOverdue);
        Assert.Equal("has_overdue", (await Assert.ThrowsAsync<DomainException>(() => reservations.CreateAsync(1, 5, default))).Code);

        // Возврат освобождает экземпляр, повтор ничего не меняет.
        await loans.ReturnAsync(loan.Id, default);
        var returned = await loans.ReturnAsync(loan.Id, default);
        Assert.Equal(LoanStatus.Returned, returned.Status);
        Assert.Equal(Now.AddDays(22), returned.ReturnedAt);
        Assert.Equal(CopyState.Available, db.Copies.Single(c => c.BookId == 1).State);
    }

    [Fact]
    public async Task Foreign_data_is_not_accessible()
    {
        var (db, clock) = Setup();
        var reservations = new ReservationService(db, clock);
        var loans = new LoanService(db, clock);
        var lists = new ReadingListService(db, clock);

        var r = await reservations.CreateAsync(1, 1, default);
        var loan = await loans.IssueAsync(r.Id, default);
        var r2 = await reservations.CreateAsync(1, 2, default);
        var list = await lists.CreateAsync(1, "Моё", default);

        await Assert.ThrowsAsync<NotFoundException>(() => reservations.CancelAsync(2, r2.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => loans.RenewAsync(2, loan.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => lists.RenameAsync(2, list.Id, "Чужое", default));
        await Assert.ThrowsAsync<NotFoundException>(() => lists.DeleteAsync(2, list.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => lists.AddBookAsync(2, list.Id, 3, default));
        Assert.Empty(await reservations.GetMineAsync(2, default));
        Assert.Empty(await loans.GetMineAsync(2, default));
        Assert.Empty(await lists.GetMineAsync(2, default));

        // Ничего не изменилось.
        Assert.Equal(ReservationStatus.Active, db.Reservations.Single(x => x.Id == r2.Id).Status);
        Assert.Equal(0, db.Loans.Single().RenewalCount);
        Assert.Equal("Моё", db.ReadingLists.Single().Name);
    }

    [Fact]
    public async Task Reading_list_names()
    {
        var (db, clock) = Setup();
        var lists = new ReadingListService(db, clock);

        var list = await lists.CreateAsync(1, "  Летнее чтение  ", default);
        Assert.Equal("Летнее чтение", list.Name);
        Assert.Equal("list_name_taken", (await Assert.ThrowsAsync<DomainException>(() => lists.CreateAsync(1, "летнее ЧТЕНИЕ", default))).Code);
        await Assert.ThrowsAsync<DomainException>(() => lists.CreateAsync(1, "   ", default));
        await Assert.ThrowsAsync<DomainException>(() => lists.CreateAsync(1, new string('я', 51), default));

        await lists.AddBookAsync(1, list.Id, 1, default);
        Assert.Equal("book_already_in_list", (await Assert.ThrowsAsync<DomainException>(() => lists.AddBookAsync(1, list.Id, 1, default))).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => lists.GetAsync(2, list.Id, default));
    }
}
