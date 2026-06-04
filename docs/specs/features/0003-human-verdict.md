# SPEC-0003 — Human verdict (approve/reject)

- **Status:** Draft
- **Phase:** MVP
- **Bounded Context:** Decision
- **Author:** DevIA Team
- **Date:** 2026-06-01

## 1. Goal

Let a Senior Developer issue the final **verdict** on a review (Approve or Reject)
with a justification, recording it auditably and reflecting the outcome back on the
GitHub PR. This is the *human-in-the-loop* decision point — the product's core value.

## 2. Context / Problem

[SPEC-0001](0001-pr-review-pipeline.md) leaves a review in `AwaitingHumanReview`.
This spec closes the loop: the human decides. Without it, the AI assessment has no
authority and nothing happens to the PR.

## 3. Actors and Scope

- **Actors:** Senior Developer (Reviewer).
- **In scope:** approve/reject a review, require justification on reject, persist
  `Verdict`, update `review.status`, write `AuditLog`, reflect outcome on GitHub.
- **Out of scope:** false-positive/valid feedback on individual findings (Phase 2,
  `finding_feedback`), auto-merge policies, multi-reviewer approval.

## 4. Expected Behavior (User Stories)

> As a **Senior Developer**, I want to approve or reject a reviewed PR with a
> justification, so that my decision is recorded and reflected on GitHub.

## 5. Scenarios (Gherkin)

```gherkin
Scenario: Approve a review
  Given a review in status "AwaitingHumanReview"
  And the current user has the Reviewer role
  When they approve it
  Then a Verdict (Approved) is recorded with the reviewer and timestamp
  And review.status becomes "Approved"
  And the outcome is reflected on the GitHub PR
  And an AuditLog entry is written

Scenario: Reject a review requires justification
  Given a review in status "AwaitingHumanReview"
  When the reviewer rejects it without a justification
  Then the request is refused with a validation error
  And no Verdict is recorded

Scenario: Verdict only once
  Given a review that already has a Verdict
  When a verdict is submitted again
  Then the request is refused (the review is already decided)

Scenario: Unauthorized user
  Given a user without the Reviewer role
  When they attempt to issue a verdict
  Then the request is refused with 403
```

## 6. Acceptance Criteria

- [ ] Only a user with the Reviewer (or Admin) role can issue a verdict.
- [ ] A verdict can only be issued when `review.status = AwaitingHumanReview`.
- [ ] Rejecting **requires** a non-empty justification; approving makes it optional.
- [ ] Issuing a verdict records a `Verdict` (decision, reviewer, justification, timestamp).
- [ ] `review.status` is updated to `Approved` / `Rejected` accordingly.
- [ ] Exactly one verdict per review (the current PR version); re-submission is refused.
- [ ] The outcome is reflected on the GitHub PR via a **Check Run** (✅/❌ conclusion) **and** a **Comment** with the assessment (which also notifies the author).
- [ ] An `AuditLog` entry is written for the decision.

## 7. Business Rules

- Verdict is **immutable** once recorded (corrections happen on a new review of a new PR version).
- `review.status` denormalizes the decision; `Verdict` holds the detail (per [data model](../../domain/data-model.md)).
- A `Blocker` finding does not block the verdict — the human always decides (human-in-the-loop).
- The platform **never merges** the PR; the verdict only signals the outcome. The human
  performs the merge on GitHub (the dashboard deep-links to the PR for convenience).

## 8. Contracts / Interfaces

- `POST /reviews/{id}/verdict` — body: `{ "decision": "Approved | Rejected", "justification": "string?" }`.
- **GitHub reflection (decided):** set a **Check Run** (✅/❌ conclusion on the PR head
  commit — can be made a *required check* to gate merge) **and** post a **Comment** with
  the assessment + verdict (which also notifies the author). A full PR review
  (approve/request-changes) is deferred to Phase 2.
- Entities: `Verdict`, `AuditLog` per [data model](../../domain/data-model.md).

## 9. Technical Considerations

- **Authorization** enforced server-side (role check), not just in the UI.
- **Idempotency / concurrency:** guard against double-submit (unique verdict per review; optimistic check on status).
- **GitHub reflection** is a side effect — if it fails after the verdict is saved,
  retry it without re-recording the verdict (the verdict is the source of truth).
- Audit entry captures actor, action, entity, and before/after status.

## 10. Test Strategy

| Layer | What to test | How |
|-------|--------------|-----|
| Unit | Role authorization, status precondition, reject-requires-justification, single-verdict rule, status transition, audit write | GitHub API mocked |
| Integration | `POST verdict` → Postgres (Verdict + status + audit) → GitHub reflection | Testcontainers (Postgres) + GitHub stub |

> No LLM involved → no evals in this spec.

## 11. Resolved Decisions

- **GitHub reflection (Q1):** Check Run + Comment. The Check Run sets a ✅/❌ conclusion
  on the PR head commit (can be a required check to gate merge); the Comment carries the
  human-readable assessment. Full PR review (approve/request-changes) → Phase 2.
- **Merge (Q2):** signal only — the platform never auto-merges. The dashboard deep-links
  to the GitHub PR so the human merges there. An in-app, human-triggered merge button is
  a possible Phase 2/3 opt-in (own spec — handles merge method, conflicts, required checks).
- **Author notification (Q3):** yes — posting the verdict as a PR Comment triggers
  GitHub's native notification to the author (resolved together with Q1).
