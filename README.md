# Booklink — серверное приложение

ASP.NET Core Web API (.NET 9) + PostgreSQL 17, Clean Architecture.

```
src/
  Booklink.Domain          сущности, состояния, правила прототипа (ТЗ 3.1.10)
  Booklink.Application     сценарии: каталог, бронирование, выдачи, списки; DTO; абстракции
  Booklink.Infrastructure  EF Core + Npgsql, миграции, JWT, хеширование паролей, фоновое истечение броней
  Booklink.Api             контроллеры, авторизация по ролям, OpenAPI (Swagger)
tests/Booklink.Tests       тесты бизнес-правил
```

## Запуск

1. PostgreSQL 17 (со сборкой ICU — стандартно для всех дистрибутивов).
2. Конфигурация — скопировать пример и заполнить:
   ```bash
   cp src/Booklink.Api/appsettings.Development.example.json src/Booklink.Api/appsettings.Development.json
   ```
   Либо переменные окружения: `ConnectionStrings__Default`, `Jwt__Key` (≥ 32 символов), `Seed__Password`.
3. Запуск (база создаётся, миграции применяются и демо-данные загружаются автоматически):
   ```bash
   dotnet run --project src/Booklink.Api --launch-profile http
   ```
4. Swagger UI: http://localhost:5232/swagger

Тесты: `dotnet test`. Новая миграция:
`dotnet ef migrations add <Имя> -p src/Booklink.Infrastructure -s src/Booklink.Api -o Persistence/Migrations`

## Демо-учётки

Пароль у всех — значение `Seed:Password`.

| Логин | Роль |
|---|---|
| student1, student2 | Student |
| librarian | Librarian |

Каталог: 25 книг. «Ревизор» — бронирование запрещено, «Идиот» — продление запрещено.

## API

Авторизация: `Authorization: Bearer <accessToken>` из `POST /api/auth/login`. Истёкший токен → `401`, нужен повторный вход.
Ошибки — ProblemDetails: `detail` — сообщение на русском, `code` — машинный код (`no_available_copies`, `reservation_limit`, `renewal_denied`, …).
Даты в UTC (ISO 8601), отображение в Europe/Moscow — на клиенте.

| Метод | Путь | Роль |
|---|---|---|
| POST | /api/auth/login, /api/auth/logout; GET /api/auth/me | все |
| GET | /api/books?search=&page= ; /api/books/{id} | все |
| POST | /api/reservations `{bookId}` ; GET /api/reservations/my ; POST /api/reservations/{id}/cancel | Student |
| GET | /api/loans/my ; POST /api/loans/{id}/renew | Student |
| GET/POST | /api/lists ; GET/PUT/DELETE /api/lists/{id} ; POST /api/lists/{id}/books `{bookId}` ; DELETE /api/lists/{id}/books/{bookId} | Student |
| GET | /api/librarian/reservations?search= ; /api/librarian/reservations/{id} ; POST …/{id}/issue | Librarian |
| GET | /api/librarian/loans?search= ; /api/librarian/loans/{id} ; POST …/{id}/return | Librarian |

## Гарантии

- Операции бронирования, выдачи, продления, возврата — одна транзакция `Serializable` с автоповтором: при одновременных запросах на последний экземпляр бронь получает только один.
- Дополнительно в БД: уникальная активная бронь и активная выдача на экземпляр, одна выдача на бронь, уникальные названия списков (без учёта регистра).
- Повторы безопасны: повторная выдача/возврат возвращают тот же результат, повторное бронирование/продление отклоняются правилами.
- Истечение броней — фоновая задача раз в 30 с + проверка при каждой операции.
- Пароли — PBKDF2 (ASP.NET Core Identity `PasswordHasher`); токены и пароли не пишутся в журнал.

## Резервное копирование

```bash
pg_dump -h localhost -U postgres -Fc booklink > booklink.dump
pg_restore -h localhost -U postgres --clean --create -d postgres booklink.dump
```
