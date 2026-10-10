namespace Booklink.Domain;

/// <summary>Нарушение бизнес-правила. Code — машинный код, Message — текст для пользователя.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public class NotFoundException(string message) : Exception(message);
