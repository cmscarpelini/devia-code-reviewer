# SPEC-0001 — PR review pipeline (webhook → assessment)

- **Status:** Draft
- **Phase:** MVP
- **Bounded Context:** Code Ingestion + Review Context
- **Author:** DevIA Team
- **Date:** 2026-06-01

## 1. Goal

When a Pull Request is opened or updated on GitHub, the system runs an automated AI
review and produces an **assessment** (executive summary + list of findings with
severity), leaving the PR ready for a Senior Developer's verdict.

## 2. Context / Problem

This is the MVP's core feature and proves the product's value. Without it, there is
no assessment for the human to decide on. Related decisions:
[ADR-0003](../../architecture/adr/0003-database-strategy-postgres-mongodb.md)
(persistence) and the async flow in [overview](../../architecture/overview.md).

## 3. Actors and Scope

- **Actors:** Developer (author), System (AI).
- **In scope:** receive webhook, fetch diff, generate summary and findings, persist
  the result, post a comment on the PR, mark it as `AwaitingHumanReview`.
- **Out of scope:** human verdict (future SPEC), configurable rules,
  feedback/learning, incremental re-review.

## 4. Expected Behavior (User Stories)

> As a **Developer**, I want my PR to receive an automatic assessment right after I
> open it, so that I quickly know what needs adjusting.

> As the **System**, I need to process the review asynchronously and resiliently,
> so that I don't block the webhook and can tolerate LLM failures.

## 5. Scenarios (Gherkin)

```gherkin
Scenario: Opened PR produces an assessment
  Given a repository connected to the platform
  When a Pull Request is opened on GitHub
  Then a Review is created with status "Pending"
  And a review job is enqueued
  And the webhook responds 202 in under 2 seconds

Scenario: Worker processes the review
  Given a review job in the queue
  When the Worker processes the job
  Then the PR diff is fetched from GitHub
  And the pipeline generates an executive summary and a list of findings
  And the raw result is stored in MongoDB
  And the Review moves to status "AwaitingHumanReview"
  And a comment with the assessment is posted on the PR

Scenario: LLM provider failure
  Given a review job in the queue
  When the LLM provider returns an error
  Then the Review is marked as "Failed"
  And the job is reprocessable (retry with backoff)
  And no partial comment is posted on the PR
```

## 6. Acceptance Criteria

- [ ] The webhook validates the GitHub signature and responds 202 without processing synchronously.
- [ ] Duplicate GitHub events are idempotent (no duplicate review created).
- [ ] The assessment contains: executive summary + list of findings, each with a severity (`Blocker|Major|Minor|Info`).
- [ ] The LLM output is **structured JSON** validated against a schema before persisting.
- [ ] The raw result (diff, prompts, response) is stored in MongoDB; metadata in Postgres.
- [ ] An LLM failure never leaves the Review in an inconsistent state; it allows retry.
- [ ] The PR comment is posted exactly once per PR version.

## 7. Business Rules

- A PR may have several Reviews over time (one per relevant push); in the MVP, the
  review of the latest version is the current one.
- A `Blocker` severity visually flags the PR as "high risk" in the dashboard.
- Secrets detected in the diff must be **redacted** before sending to the LLM.

## 8. Contracts / Interfaces

**Inbound webhook (GitHub → API):** `pull_request` event (`opened`, `synchronize`).

**Expected LLM output schema (abridged):**

```json
{
  "summary": "string",
  "findings": [
    {
      "title": "string",
      "severity": "Blocker | Major | Minor | Info",
      "category": "Bug | Security | Style | Performance | Test",
      "file": "string",
      "line": 0,
      "description": "string",
      "suggestion": "string"
    }
  ]
}
```

## 9. Technical Considerations

- **Async:** the webhook only validates and enqueues; the Worker does the heavy lifting.
- **Idempotency:** a key per (repo, PR, head_sha) prevents duplicates.
- **Resilience:** exponential backoff retry on transient LLM failures.
- **Cost:** record tokens consumed per review for future control.
- **Orchestration:** pipeline in Semantic Kernel — steps summarize → analyze → consolidate.

## 10. Test Strategy

| Layer | What to test | How |
|-------|--------------|-----|
| Unit | Signature validation, prompt assembly, JSON parsing/validation, severity mapping, idempotency | **LLM mocked** |
| Integration | webhook → queue → worker → Postgres/Mongo → comment (GitHub mocked) | Test environment with fixtures |
| **Eval** | Assessment quality: does the AI find the known problems? | **Golden dataset** of PRs with labeled defects; precision/recall and false-positive-rate metrics |

> The eval set runs as a **regression test** on every prompt or model change.

## 11. Open Questions

- Which LLM provider for the MVP (Azure OpenAI vs. OpenAI vs. Anthropic)?
- Maximum diff size before chunking the PR?
- Secret-redaction strategy: custom regex or a dedicated tool?
