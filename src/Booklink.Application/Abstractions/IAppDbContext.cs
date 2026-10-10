using Booklink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Book> Books { get; }
    DbSet<Copy> Copies { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<Loan> Loans { get; }
    DbSet<ReadingList> ReadingLists { get; }
    DbSet<ReadingListItem> ReadingListItems { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Выполняет действие в одной сериализуемой транзакции; при конфликте конкурентных
    /// запросов транзакция повторяется целиком. Действие должно быть повторяемым.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
