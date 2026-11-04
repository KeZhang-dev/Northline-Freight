namespace Northline.Api.Common;

public record RuleViolation(string Rule, string Code, string Message);

/// <summary>
/// A business or state error that is returned to the client as an RFC 7807 problem response.
/// Thrown by services; mapped by DomainExceptionHandler. Never leaves partial changes behind,
/// because services throw before committing their transaction.
/// </summary>
public class DomainException(
    int statusCode,
    string code,
    string title,
    string detail,
    IReadOnlyList<RuleViolation>? violations = null) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public IReadOnlyList<RuleViolation> Violations { get; } = violations ?? [];

    public static DomainException NotFound(string code, string detail) =>
        new(StatusCodes.Status404NotFound, code, "Resource not found", detail);

    public static DomainException Conflict(string code, string detail, string? rule = null) =>
        new(StatusCodes.Status409Conflict, code, "Conflict", detail,
            rule is null ? null : [new RuleViolation(rule, code, detail)]);

    public static DomainException RuleViolations(IReadOnlyList<RuleViolation> violations) =>
        new(StatusCodes.Status422UnprocessableEntity, ErrorCodes.AssignmentValidationFailed,
            "Assignment validation failed",
            violations.Count == 1
                ? violations[0].Message
                : $"{violations.Count} business rules were violated. See 'violations' for details.",
            violations);
}
