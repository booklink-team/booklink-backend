using System.ComponentModel.DataAnnotations;
using Booklink.Application;
using Booklink.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booklink.Api.Controllers;

public record CreateReservationRequest([Required] int? BookId);
public record ListNameRequest([Required] string Name);
public record AddBookRequest([Required] int? BookId);

[ApiController, Route("api/reservations"), Authorize(Roles = Roles.Student)]
public class ReservationsController(ReservationService reservations) : ControllerBase
{
    /// <summary>Бронирование книги; экземпляр выбирается автоматически. 409 — отказ с причиной в detail.</summary>
    [HttpPost]
    public Task<ReservationDto> Create(CreateReservationRequest req, CancellationToken ct) =>
        reservations.CreateAsync(User.Id(), req.BookId!.Value, ct);

    [HttpGet("my")]
    public Task<IReadOnlyList<ReservationDto>> Mine(CancellationToken ct) => reservations.GetMineAsync(User.Id(), ct);

    [HttpPost("{id:int}/cancel")]
    public Task<ReservationDto> Cancel(int id, CancellationToken ct) => reservations.CancelAsync(User.Id(), id, ct);
}

[ApiController, Route("api/loans"), Authorize(Roles = Roles.Student)]
public class LoansController(LoanService loans) : ControllerBase
{
    /// <summary>Действующие выдачи и история возвратов. renewedDueAt — новая дата при продлении.</summary>
    [HttpGet("my")]
    public Task<IReadOnlyList<LoanDto>> Mine(CancellationToken ct) => loans.GetMineAsync(User.Id(), ct);

    /// <summary>Продление на 7 дней. 409 — отказ, срок не меняется.</summary>
    [HttpPost("{id:int}/renew")]
    public Task<LoanDto> Renew(int id, CancellationToken ct) => loans.RenewAsync(User.Id(), id, ct);
}

[ApiController, Route("api/lists"), Authorize(Roles = Roles.Student)]
public class ReadingListsController(ReadingListService lists) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReadingListDto>> Mine(CancellationToken ct) => lists.GetMineAsync(User.Id(), ct);

    [HttpGet("{id:int}")]
    public Task<ReadingListDetailsDto> Get(int id, CancellationToken ct) => lists.GetAsync(User.Id(), id, ct);

    [HttpPost]
    public Task<ReadingListDto> Create(ListNameRequest req, CancellationToken ct) => lists.CreateAsync(User.Id(), req.Name, ct);

    [HttpPut("{id:int}")]
    public Task<ReadingListDto> Rename(int id, ListNameRequest req, CancellationToken ct) => lists.RenameAsync(User.Id(), id, req.Name, ct);

    [HttpDelete("{id:int}")]
    public async Task<NoContentResult> Delete(int id, CancellationToken ct)
    {
        await lists.DeleteAsync(User.Id(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/books")]
    public async Task<NoContentResult> AddBook(int id, AddBookRequest req, CancellationToken ct)
    {
        await lists.AddBookAsync(User.Id(), id, req.BookId!.Value, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/books/{bookId:int}")]
    public async Task<NoContentResult> RemoveBook(int id, int bookId, CancellationToken ct)
    {
        await lists.RemoveBookAsync(User.Id(), id, bookId, ct);
        return NoContent();
    }
}
