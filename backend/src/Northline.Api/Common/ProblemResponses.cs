using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Northline.Api.Common;

public static class ProblemResponses
{
    public static ProblemDetails Create(
        HttpContext context, int status, string code, string title, string detail,
        IReadOnlyList<RuleViolation>? violations = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.io/{status}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["violations"] = violations ?? [];
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static Task WriteAsync(
        HttpContext context, int status, string code, string title, string detail,
        IReadOnlyList<RuleViolation>? violations = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            Create(context, status, code, title, detail, violations),
            options: null, contentType: "application/problem+json");
    }
}

/// <summary>Maps DomainException (and unexpected errors) to problem responses.</summary>
public class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DomainException domain)
        {
            await ProblemResponses.WriteAsync(context, domain.StatusCode, domain.Code, domain.Title, domain.Message, domain.Violations);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        await ProblemResponses.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
            "Internal server error", "An unexpected error occurred. Quote the traceId when reporting this problem.");
        return true;
    }
}
