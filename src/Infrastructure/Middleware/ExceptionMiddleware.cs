using Microsoft.Data.SqlClient;
using Npgsql;
using System.Diagnostics;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Boilerate.Infrastructure.Middleware;
internal sealed class ExceptionMiddleware(ILogger<ExceptionMiddleware> logger, IHostEnvironment environment) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; do not attempt to write a second/error response to the closed transport.
            context.Abort();
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            var status = ex switch
            {
                CustomException ce => (int)ce.StatusCode,
                DomainException de => (int)de.StatusCode,
                ValidationException => 400,
                DbUpdateConcurrencyException => 409,
                DbUpdateException update when IsConstraintConflict(update.InnerException) => 409,
                KeyNotFoundException => 404,
                _ => 500
            };
            var trace = Activity.Current?.Id ?? context.TraceIdentifier;
            logger.LogError(ex, "Request failed: {TraceId}, status {StatusCode}", trace, status);
            var problem = new ProblemDetails
            {
                Status = status, Title = status >= 500 ? "An unexpected error occurred." : "Request rejected.",
                Detail = status < 500 && (ex is CustomException or DomainException)
                    ? ex.Message
                    : environment.IsDevelopment() ? ex.Message : null,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = trace;
            problem.Extensions["code"] = status switch { 400 => "validation_failed", 401 => "unauthorized", 403 => "forbidden", 404 => "not_found", 409 => "conflict", _ => "internal_error" };
            if (ex is ValidationException validation) problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json", context.RequestAborted);
        }
    }
    private static bool IsConstraintConflict(Exception? exception) => exception switch
    {
        SqlException sql => sql.Number is 2601 or 2627 or 547,
        PostgresException postgres => postgres.SqlState is PostgresErrorCodes.UniqueViolation or
            PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ExclusionViolation,
        _ => false
    };

}
