namespace Booklink.Domain;

/// <summary>Правила прототипа, ТЗ п. 3.1.10.</summary>
public static class LibraryRules
{
    public static readonly TimeSpan ReservationTerm = TimeSpan.FromHours(48);
    public const int MaxActiveReservations = 3;
    public const int MaxActiveLoans = 5;
    public static readonly TimeSpan LoanTerm = TimeSpan.FromDays(14);
    public const int MaxRenewals = 1;
    public static readonly TimeSpan RenewalTerm = TimeSpan.FromDays(7);
    public const int PageSize = 20;
    public const int ListNameMaxLength = 50;
}
