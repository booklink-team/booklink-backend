using Booklink.Application;
using Booklink.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booklink.Api.Controllers;

[ApiController, Route("api/librarian"), Authorize(Roles = Roles.Librarian)]
public class LibrarianController(ReservationService reservations, LoanService loans) : ControllerBase
{
    /// <summary>Активные бронирования. search — номер брони, имя или логин читателя.</summary>
    [HttpGet("reservations")]
    public Task<IReadOnlyList<ReservationDto>> Reservations(string? search, CancellationToken ct) =>
        reservations.SearchActiveAsync(search, ct);

    [HttpGet("reservations/{id:int}")]
    public Task<ReservationDto> Reservation(int id, CancellationToken ct) => reservations.GetAsync(id, ct);

    /// <summary>Подтверждение выдачи по брони. Повторный вызов возвращает ту же выдачу.</summary>
    [HttpPost("reservations/{id:int}/issue")]
    public Task<LoanDto> Issue(int id, CancellationToken ct) => loans.IssueAsync(id, ct);

    /// <summary>Действующие выдачи. search — читатель, название книги или инвентарный номер.</summary>
    [HttpGet("loans")]
    public Task<IReadOnlyList<LoanDto>> Loans(string? search, CancellationToken ct) => loans.SearchActiveAsync(search, ct);

    [HttpGet("loans/{id:int}")]
    public Task<LoanDto> Loan(int id, CancellationToken ct) => loans.GetAsync(id, ct);

    /// <summary>Подтверждение возврата. Повторный вызов не создаёт новую операцию.</summary>
    [HttpPost("loans/{id:int}/return")]
    public Task<LoanDto> Return(int id, CancellationToken ct) => loans.ReturnAsync(id, ct);
}
