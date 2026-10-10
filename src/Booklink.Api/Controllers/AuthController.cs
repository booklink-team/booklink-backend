using System.ComponentModel.DataAnnotations;
using Booklink.Application;
using Booklink.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booklink.Api.Controllers;

public record LoginRequest([Required] string Login, [Required] string Password);

[ApiController, Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Вход по логину и паролю. 401 — неверные учётные данные.</summary>
    [HttpPost("login")]
    public Task<LoginResult> Login(LoginRequest req, CancellationToken ct) => auth.LoginAsync(req.Login, req.Password, ct);

    /// <summary>Текущий пользователь и его роль. 401 — сеанс истёк, нужен повторный вход.</summary>
    [HttpGet("me"), Authorize]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetAsync(User.Id(), ct);

    /// <summary>Выход. Токен без состояния — клиент удаляет его из защищённого хранилища.</summary>
    [HttpPost("logout"), Authorize]
    public NoContentResult Logout() => NoContent();
}
