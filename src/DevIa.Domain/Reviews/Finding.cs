using DevIa.Domain.Common;
using DevIa.Domain.Enums;

namespace DevIa.Domain.Reviews;

/// <summary>
/// An individual issue identified in a review. Created as part of the <see cref="Review"/>
/// aggregate; the <c>ReviewId</c> foreign key is set by EF via the relationship.
/// </summary>
public sealed class Finding : Entity
{
    public Guid ReviewId { get; private set; }
    public Severity Severity { get; private set; }
    public FindingCategory Category { get; private set; }
    public string FilePath { get; private set; } = null!;
    public int? Line { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? Suggestion { get; private set; }

    private Finding() { } // EF

    public Finding(
        Severity severity,
        FindingCategory category,
        string filePath,
        int? line,
        string title,
        string description,
        string? suggestion = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new DomainException("FilePath is required.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Description is required.");
        if (line is < 0) throw new DomainException("Line cannot be negative.");

        Id = Guid.NewGuid();
        Severity = severity;
        Category = category;
        FilePath = filePath;
        Line = line;
        Title = title;
        Description = description;
        Suggestion = suggestion;
    }
}
