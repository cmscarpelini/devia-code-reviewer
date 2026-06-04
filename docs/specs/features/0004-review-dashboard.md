# SPEC-0004 — Review dashboard (queue & assessment view)

- **Status:** Draft
- **Phase:** MVP
- **Bounded Context:** Review + Decision (frontend; reads via API)
- **Author:** DevIA Team
- **Date:** 2026-06-01

## 1. Goal

Give the Senior Developer a dashboard to see the **queue** of PRs awaiting review and
open an **assessment** (summary + findings) to decide on. This is the human's primary
workspace — the surface that makes [SPEC-0003](0003-human-verdict.md) usable.

## 2. Context / Problem

The AI produces assessments (SPEC-0001) and the human issues verdicts (SPEC-0003),
but the reviewer needs a place to actually see what needs deciding and act on it.
Without this, the loop has no usable UI.

## 3. Actors and Scope

- **Actors:** Senior Developer (acts), Tech Lead (views).
- **In scope:** a queue list (filter by status), a review detail view (summary +
  findings grouped by severity, link to the GitHub PR), and the approve/reject action
  surfaced from the detail.
- **Out of scope:** metrics/trend dashboards (Phase 2), rule configuration UI,
  per-finding feedback UI (Phase 2).

## 4. Expected Behavior (User Stories)

> As a **Senior Developer**, I want to see all PRs awaiting my review and open each
> one's assessment, so that I can quickly decide.

> As a **Senior Developer**, I want findings grouped by severity with file/line, so
> that I focus on what matters first.

## 5. Scenarios (Gherkin)

```gherkin
Scenario: See the review queue
  Given reviews exist in various statuses
  When the reviewer opens the dashboard
  Then they see reviews with status "AwaitingHumanReview" by default
  And each item shows the repository, PR title, author, and risk indicator

Scenario: Open an assessment
  Given a review awaiting human review
  When the reviewer opens it
  Then they see the executive summary
  And the findings grouped by severity (Blocker → Info) with file and line
  And a link to the GitHub PR

Scenario: Decide from the detail view
  Given an open assessment
  When the reviewer approves or rejects (with justification if rejecting)
  Then the verdict is submitted (SPEC-0003)
  And the item leaves the "awaiting" queue
```

## 6. Acceptance Criteria

- [ ] The queue lists reviews, defaulting to `AwaitingHumanReview`, with a status filter.
- [ ] Each queue item shows repository, PR title, author, and a risk/severity indicator.
- [ ] A `Blocker` finding visibly flags the item as high-risk.
- [ ] The detail view shows the executive summary and findings grouped by severity, each with file/line, category, description, and suggestion.
- [ ] The detail view links to the GitHub PR.
- [ ] Approve/reject is available from the detail and calls SPEC-0003 (with justification required on reject).
- [ ] Access is restricted to authenticated users; verdict actions require the Reviewer role.
- [ ] The list is paginated.

## 7. Business Rules

- A reviewer acts on the **current** review of a PR (latest version).
- Findings are ordered by severity: `Blocker` → `Major` → `Minor` → `Info`.
- After a verdict, the review moves out of the default queue.

## 8. Contracts / Interfaces

- `GET /reviews?status=AwaitingHumanReview&page=&pageSize=` → paginated queue (metadata from Postgres).
- `GET /reviews/{id}` → assessment detail (summary + findings; raw content from Mongo if needed).
- Verdict action reuses `POST /reviews/{id}/verdict` (SPEC-0003).
- Frontend: Next.js + React + TS, per [overview](../../architecture/overview.md).

## 9. Technical Considerations

- Reads come from Postgres (queue/metadata + mirrored findings); the full raw result
  is fetched from Mongo only when needed for detail.
- Authentication via the JWT from [SPEC-0002](0002-github-onboarding.md); authorization for verdict actions.
- Pagination and sensible defaults to keep the queue responsive.
- Empty/loading/error states defined for a usable UX.

## 10. Test Strategy

| Layer | What to test | How |
|-------|--------------|-----|
| Unit (frontend) | Findings grouping/ordering by severity, risk indicator, gated verdict action | Component tests with mocked API |
| Unit (API) | Query filtering, pagination, authorization on list/detail | Repositories mocked |
| Integration | `GET /reviews` and `GET /reviews/{id}` return correct shape from Postgres/Mongo | Testcontainers + seeded data |

> No LLM involved → no evals in this spec.

## 11. Open Questions

- Real-time updates (e.g., a new review appears) in the MVP, or refresh-on-load is enough?
- Does the Tech Lead see the same queue read-only, or a separate view in the MVP?
- Do we show the raw diff in the detail view, or only the findings + link to GitHub?
