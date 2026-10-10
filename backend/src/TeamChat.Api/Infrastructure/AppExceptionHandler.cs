using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Common;

namespace TeamChat.Api.Infrastructure;

public sealed class AppExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AppExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public AppExceptionHandler(ILogger<AppExceptionHandler> logger, IProblemDetailsService problemDetails)
    {
        _logger = logger;
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is FluentValidation.ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest },
            });
        }

        var (status, title) = exception switch
        {
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Non autorise"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Interdit"),
            BadRequestException => (StatusCodes.Status400BadRequest, "Requete invalide"),
            NotFoundException => (StatusCodes.Status404NotFound, "Ressource introuvable"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflit"),
            _ => (StatusCodes.Status500InternalServerError, "Erreur serveur"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Erreur non geree");
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Une erreur interne est survenue."
                    : exception.Message,
            },
        });
    }
}
