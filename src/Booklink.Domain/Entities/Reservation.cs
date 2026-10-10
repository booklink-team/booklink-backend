namespace Booklink.Domain.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CopyId { get; set; }
    public Copy Copy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; }

    public bool IsLive(DateTime now) => Status == ReservationStatus.Active && ExpiresAt > now;

    public void Cancel(DateTime now)
    {
        if (!IsLive(now))
            throw new DomainException("reservation_not_active", "Бронирование не активно.");
        Status = ReservationStatus.Cancelled;
        Copy.State = CopyState.Available;
    }

    public Loan Issue(DateTime now)
    {
        if (Status == ReservationStatus.Active && ExpiresAt <= now)
            throw new DomainException("reservation_expired", "Срок бронирования истёк.");
        if (Status != ReservationStatus.Active)
            throw new DomainException("reservation_not_active", "Бронирование не активно.");
        if (Copy.State != CopyState.Reserved)
            throw new DomainException("copy_unavailable", "Экземпляр недоступен для выдачи.");

        Status = ReservationStatus.Completed;
        Copy.State = CopyState.Issued;
        return new Loan
        {
            UserId = UserId,
            CopyId = CopyId,
            Copy = Copy,
            ReservationId = Id,
            IssuedAt = now,
            DueAt = now + LibraryRules.LoanTerm,
            Status = LoanStatus.Active,
        };
    }
}
