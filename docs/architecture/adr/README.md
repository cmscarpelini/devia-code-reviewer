# Architecture Decision Records (ADR)

A record of the project's relevant architectural decisions. Each decision is a
numbered, immutable file: superseded decisions get the status `Superseded` and
point to the decision that replaced them.

## Format

Each ADR follows: **Context → Decision → Consequences**, with a `Status`.

Possible statuses: `Proposed` · `Accepted` · `Deprecated` · `Superseded`.

## Index

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-record-architecture-decisions.md) | Adopt ADRs to record decisions | Accepted |
| [0002](0002-spec-driven-development.md) | Spec-Driven Development as the base methodology | Accepted |
| [0003](0003-database-strategy-postgres-mongodb.md) | Database strategy: PostgreSQL + MongoDB | Accepted |
| [0004](0004-provider-agnostic-explicit-pipeline.md) | Provider-agnostic, explicitly-orchestrated review pipeline | Accepted |
| [0005](0005-evaluation-strategy-for-ai-review.md) | Evaluation strategy for the AI review (tests + evals) | Accepted |

## How to create a new ADR

1. Copy the structure of an existing ADR.
2. Number it sequentially (`NNNN-title-in-kebab-case.md`).
3. Start with status `Proposed`; change to `Accepted` once decided.
4. Add the row to the index above.
