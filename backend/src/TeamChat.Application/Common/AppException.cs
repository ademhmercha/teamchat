namespace TeamChat.Application.Common;

/// <summary>Erreur metier generique de la couche Application.</summary>
public class AppException : Exception
{
    public AppException(string message) : base(message)
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }
}
