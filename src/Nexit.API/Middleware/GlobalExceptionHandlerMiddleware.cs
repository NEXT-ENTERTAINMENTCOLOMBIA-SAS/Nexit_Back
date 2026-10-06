using Microsoft.EntityFrameworkCore;
using Nexit.API.Models;
using Nexit.Core.Exceptions;
using Npgsql;

namespace Nexit.API.Middleware;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
            var (statusCode, message) = exception switch
            {
                EntityNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ForbiddenOperationException => (StatusCodes.Status403Forbidden, exception.Message),
                BusinessRuleException => (StatusCodes.Status409Conflict, exception.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Otra persona modificó este registro mientras lo editabas. Recarga los datos e inténtalo de nuevo."),
                DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (StatusCodes.Status409Conflict, "El registro ya existe o entra en conflicto con uno existente."),
                DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } => (StatusCodes.Status409Conflict, "La operación viola una relación requerida."),
                DbUpdateException => (StatusCodes.Status409Conflict, "La operación no pudo completarse por una restricción de datos."),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno.")
            };
            // Solo los errores inesperados (500) se reportan a Sentry; los 404/409 son parte del flujo normal. Sin DSN configurado esto no hace nada.
            if (statusCode == StatusCodes.Status500InternalServerError) SentrySdk.CaptureException(exception);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse { StatusCode = statusCode, Message = message, TraceId = context.TraceIdentifier });
        }
    }
}
