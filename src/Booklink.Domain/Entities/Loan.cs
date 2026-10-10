namespace Booklink.Domain.Entities;

public class Loan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CopyId { get; set; }
    public Copy Copy { get; set; } = null!;
    public int ReservationId { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public int RenewalCount { get; set; }
    public LoanStatus Status { get; set; }

    public bool IsOverdue(DateTime now) => Status == LoanStatus.Active && DueAt < now;

    /// <summary>Причина, по которой продление недоступно, или null.</summary>
    public string? RenewalDenyReason(DateTime now, bool userHasOverdue)
    {
        if (Status != LoanStatus.Active) return "Выдача завершена.";
        if (Copy.Book.RenewalForbidden) return "Для этой книги продление запрещено.";
        if (RenewalCount >= LibraryRules.MaxRenewals) return "Выдача уже продлевалась.";
        if (userHasOverdue) return "Есть просроченная выдача.";
        return null;
    }

    public void Renew(DateTime now, bool userHasOverdue)
    {
        if (RenewalDenyReason(now, userHasOverdue) is { } reason)
            throw new DomainException("renewal_denied", reason);
        DueAt += LibraryRules.RenewalTerm;
        RenewalCount++;
    }

    public void Return(DateTime now)
    {
        if (Status == LoanStatus.Returned) return; // повторное подтверждение — без новой операции
        Status = LoanStatus.Returned;
        ReturnedAt = now;
        Copy.State = CopyState.Available;
    }
}
