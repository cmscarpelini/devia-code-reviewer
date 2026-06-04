namespace DevIa.Domain.Common;

/// <summary>
/// Raised when a domain invariant or rule is violated.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
