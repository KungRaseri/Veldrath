namespace Veldrath.Contracts.Content;

/// <summary>One finding from the content audit, as the portal shows it.</summary>
/// <param name="Rule">Stable machine-readable rule id, e.g. <c>missing-quest-target</c>.</param>
/// <param name="Message">Human-readable description naming both sides of the problem.</param>
/// <param name="Severity"><c>Warning</c> or <c>Error</c>.</param>
/// <param name="Field">Dotted field path the issue belongs to, when it points at one.</param>
/// <param name="Identifier">Slug of the entity that owns the issue.</param>
public record ContentAuditIssueDto(
    string Rule,
    string Message,
    string Severity,
    string? Field,
    string? Identifier);
