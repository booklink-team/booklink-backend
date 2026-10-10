using System.Data;
using Booklink.Application.Abstractions;
using Booklink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booklink.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Copy> Copies => Set<Copy>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<ReadingList> ReadingLists => Set<ReadingList>();
    public DbSet<ReadingListItem> ReadingListItems => Set<ReadingListItem>();

    public Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null || !Database.IsRelational()) return action();

        // Serializable + повтор при 40001: из двух одновременных запросов на последний экземпляр
        // успешно завершится только один, второй повторится и увидит актуальное состояние.
        return Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ChangeTracker.Clear();
            await using var tx = await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await tx.CommitAsync(ct);
            return result;
        });
    }

    // lower() для кириллицы не зависит от локали БД (при LC_CTYPE=C работает только ASCII).
    private const string Icu = "und-x-icu";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Login).IsUnique();
            e.Property(u => u.Name).HasMaxLength(200).UseCollation(Icu);
            e.Property(u => u.Login).HasMaxLength(100).UseCollation(Icu);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Book>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).UseCollation(Icu);
            e.Property(x => x.Authors).HasMaxLength(300).UseCollation(Icu);
            e.HasIndex(x => x.Title);
        });

        b.Entity<Copy>(e =>
        {
            e.HasIndex(c => c.InventoryNumber).IsUnique();
            e.Property(c => c.InventoryNumber).HasMaxLength(50).UseCollation(Icu);
            e.Property(c => c.State).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Reservation>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            // Один экземпляр — не более одной активной брони.
            e.HasIndex(r => r.CopyId).IsUnique().HasFilter("\"Status\" = 'Active'");
            e.HasIndex(r => new { r.UserId, r.Status });
            e.HasIndex(r => new { r.Status, r.ExpiresAt });
        });

        b.Entity<Loan>(e =>
        {
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(l => l.CopyId).IsUnique().HasFilter("\"Status\" = 'Active'");
            e.HasIndex(l => l.ReservationId).IsUnique();
            e.HasOne<Reservation>().WithMany().HasForeignKey(l => l.ReservationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.UserId, l.Status });
        });

        b.Entity<ReadingList>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(50);
            e.Property(l => l.NormalizedName).HasMaxLength(50);
            e.HasIndex(l => new { l.OwnerId, l.NormalizedName }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(l => l.OwnerId);
            e.HasMany(l => l.Items).WithOne().HasForeignKey(i => i.ReadingListId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ReadingListItem>().HasKey(i => new { i.ReadingListId, i.BookId });
    }
}
