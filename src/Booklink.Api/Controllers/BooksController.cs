using Booklink.Application;
using Booklink.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booklink.Api.Controllers;

[ApiController, Route("api/books"), Authorize]
public class BooksController(CatalogService catalog) : ControllerBase
{
    /// <summary>Каталог, по 20 записей. search — часть названия или имени автора, без учёта регистра.</summary>
    [HttpGet]
    public Task<PagedResult<BookListItemDto>> Search(string? search, int page = 1, CancellationToken ct = default) =>
        catalog.SearchAsync(search, page, ct);

    [HttpGet("{id:int}")]
    public Task<BookDto> Get(int id, CancellationToken ct) => catalog.GetAsync(id, ct);
}
