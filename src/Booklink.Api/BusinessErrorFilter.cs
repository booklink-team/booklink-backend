using Booklink.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Booklink.Api;

/// <summary>
/// Отказы по бизнес-правилам → ProblemDetails с полями code и detail (сообщение на русском).
/// Это штатные ответы, поэтому они не попадают в журнал как ошибки сервера.
/// </summary>
public class BusinessErrorFilter(ProblemDetailsFactory problems) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, code, message) = context.Exception switch
        {
            DomainException { Code: "invalid_credentials" } d => (401, d.Code, d.Message),
            DomainException { Code: "invalid_list_name" } d => (400, d.Code, d.Message),
            DomainException d => (409, d.Code, d.Message),
            NotFoundException n => (404, "not_found", n.Message),
            // Гонка параллельных запросов: дубль, удалённая связанная запись или исчерпаны повторы транзакции.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } }
                or RetryLimitExceededException =>
                (409, "conflict", "Операция конфликтует с текущим состоянием. Обновите данные и повторите."),
            _ => (0, "", ""),
        };
        if (status == 0) return;

        var problem = problems.CreateProblemDetails(context.HttpContext, status, message, detail: message);
        problem.Extensions["code"] = code;
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
