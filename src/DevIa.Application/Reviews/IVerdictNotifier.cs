using DevIa.Domain.Enums;

namespace DevIa.Application.Reviews;

/// <summary>The outcome to reflect on the GitHub PR (SPEC-0003): a Check Run + a Comment.</summary>
public sealed record VerdictNotification(
    string RepoFullName,
    int PrNumber,
    string HeadSha,
    VerdictDecision Decision,
    string? Justification);

/// <summary>
/// Reflects a verdict on the GitHub PR (Check Run ✅/❌ + Comment, which also notifies the
/// author). Stubbed until onboarding provides authenticated GitHub access.
/// </summary>
public interface IVerdictNotifier
{
    Task PublishAsync(VerdictNotification notification, CancellationToken cancellationToken = default);
}
