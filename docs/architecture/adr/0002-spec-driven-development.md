# ADR-0002 — Spec-Driven Development as the base methodology

- **Status:** Accepted
- **Date:** 2026-06-01

## Context

DevIA Code Reviewer will be built with heavy support from **AI agents** (e.g.,
Claude Code). Agents produce better results when given clear, durable context. We
need a methodology that: (a) reduces ambiguity, (b) serves as context for the AI,
and (c) keeps traceability between intent and implementation.

We considered alternatives:

| Approach | Limitation |
|----------|-----------|
| Code-first / ad hoc | Intent stays implicit; hard for AI and onboarding |
| Pure Spec-Driven | Covers *what*, but not the *why* nor the domain model |
| DDD only | Models the domain, but doesn't define per-feature behavior |

## Decision

Adopt **Spec-Driven Development (SDD)** as the backbone, complemented by **DDD**
(modeling), **C4** (diagrams), and **ADR** (decisions).

Every feature starts from a spec in [`specs/features/`](../../specs/features/),
written from the [template](../../specs/templates/feature-spec-template.md), before
becoming code. The spec is the source of truth.

## Consequences

**Positive**
- Features become contracts readable by humans and AI.
- Drift between code and spec is detectable (it's a bug).
- The spec's acceptance criteria drive tests and evals.

**Negative / costs**
- Requires writing the spec before coding (upfront discipline).
- Specs must be kept in sync with the code.

## References

- Detailed methodology in [methodology.md](../../methodology.md).
