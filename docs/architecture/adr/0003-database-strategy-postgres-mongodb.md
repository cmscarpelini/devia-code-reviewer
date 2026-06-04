# ADR-0003 — Database strategy: PostgreSQL + MongoDB

- **Status:** Accepted
- **Date:** 2026-06-01

## Context

The system handles two very different kinds of data:

1. **Relational, transactional data** — users, repositories, configuration,
   verdicts, and the audit trail. These require referential integrity and
   structured queries.
2. **Large, variable-schema documents** — diffs, prompts sent to the LLM, raw
   responses, review findings. These vary in size and shape with each model and
   prompt version.

Forcing both into the same database creates friction: either we lose integrity
(everything in NoSQL) or we suffer with giant JSON columns and fragile migrations
(everything relational).

## Decision

Adopt a **polyglot persistence strategy**:

| Data | Database | Reason |
|------|----------|--------|
| Users, repos, configs, reviews (metadata), verdicts, audit | **PostgreSQL** (EF Core) | Integrity, transactions, relational queries |
| Diff, prompts, LLM responses, raw review result | **MongoDB** | Flexible documents, variable schema, volume |

A `Review` keeps its **metadata** in Postgres (status, author, verdict) and its
**raw content** in Mongo, linked by an identifier.

## Consequences

**Positive**
- Each data type in the most suitable store.
- Audit and reporting are simple in the relational store.
- Evolving the review format doesn't break the relational schema.

**Negative / costs**
- Two databases to operate and monitor.
- Consistency between the two is eventual; requires care (e.g., outbox/idempotency).
- No distributed transaction across Postgres and Mongo — design to tolerate this.
