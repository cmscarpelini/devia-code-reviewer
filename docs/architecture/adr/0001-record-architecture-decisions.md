# ADR-0001 — Adopt ADRs to record architectural decisions

- **Status:** Accepted
- **Date:** 2026-06-01

## Context

Architectural decisions made without a record get lost over time. New members
(humans or AI agents) don't understand *why* something was done a certain way, and
the team repeats discussions that were already settled.

## Decision

Adopt **Architecture Decision Records (ADR)** as a lightweight mechanism to record
relevant decisions. Each ADR is a short, numbered Markdown file versioned alongside
the code, with the structure: Context → Decision → Consequences.

ADRs are immutable: a revisited decision produces a new ADR that marks the previous
one as `Superseded`.

## Consequences

**Positive**
- Long-term memory of decisions and their trade-offs.
- Faster onboarding (including AI agents, which read ADRs as context).

**Negative / costs**
- A small amount of discipline is needed to record each relevant decision.
