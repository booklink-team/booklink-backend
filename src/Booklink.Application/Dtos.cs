using Booklink.Domain;

namespace Booklink.Application;

public record UserDto(int Id, string Name, string Login, UserRole Role);
public record LoginResult(string AccessToken, DateTime ExpiresAt, UserDto User);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public record BookListItemDto(int Id, string Title, string Authors, int? Year, string? CoverUrl, int AvailableCopies);
public record BookDto(
    int Id, string Title, string Authors, string? Description, int? Year, string? CoverUrl,
    int AvailableCopies, int TotalCopies, bool ReservationForbidden, bool RenewalForbidden);

public record BookShortDto(int Id, string Title, string Authors, string? CoverUrl);
public record ReaderDto(int Id, string Name, string Login);

public record ReservationDto(
    int Id, BookShortDto Book, string InventoryNumber, DateTime CreatedAt, DateTime ExpiresAt,
    ReservationStatus Status, ReaderDto? Reader);

public record LoanDto(
    int Id, BookShortDto Book, string InventoryNumber, DateTime IssuedAt, DateTime DueAt, DateTime? ReturnedAt,
    int RenewalCount, LoanStatus Status, bool IsOverdue, bool CanRenew, string? RenewalDenyReason,
    DateTime? RenewedDueAt, ReaderDto? Reader);

public record ReadingListDto(int Id, string Name, int BookCount, DateTime CreatedAt);
public record ReadingListDetailsDto(int Id, string Name, DateTime CreatedAt, IReadOnlyList<BookListItemDto> Books);
