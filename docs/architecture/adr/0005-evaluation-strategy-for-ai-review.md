# ADR-0005 — Evaluation strategy for the AI review

- **Status:** Accepted
- **Date:** 2026-06-01

## Context

The core output of the product (the review assessment) is produced by an LLM and is
**non-deterministic**. Exact-match unit tests cannot verify its quality. We still
need confidence that (a) our code is correct and (b) the AI actually catches real
problems — and that changes to prompts/models don't silently degrade quality.

## Decision

Adopt a **two-halves** testing strategy:

1. **Deterministic half** — unit and integration tests with the **LLM mocked**
   (`IChatCompletionService` substitute). Verify our code (prompt assembly, chunking,
   validation, redaction, idempotency, persistence ordering, error paths). Run on
   every PR.
2. **AI-quality half** — **evals** over a versioned **golden dataset** of PRs with
   labeled defects. Measure **recall, precision, F1, false-positive rate, severity
   accuracy**. Use **LLM-as-judge** (rubric 1–5) for open-ended summary quality.

The eval set is a **regression suite**: prompt/model changes must run it and meet
threshold gates (e.g., recall not regressing, FP rate within target, cost within
budget). Every production miss/false-positive becomes a new dataset case.

## Consequences

**Positive**
- Clear separation of "is the code right" vs. "is the AI good".
- Quantifiable, trackable AI quality; safe prompt/model evolution.
- Deterministic tests stay fast and free (mocked LLM).

**Negative / costs**
- Building and curating the golden dataset is ongoing effort.
- Evals call a real LLM → cost; mitigated by running only on AI-affecting changes,
  using the cheap provider, caching, and smoke subsets locally.
- Precision/FP metrics require exhaustively-labeled or "clean" PRs to be meaningful.

## References

- Detail: [testing-and-evals.md](../testing-and-evals.md)
- Related: [SPEC-0001 §10](../../specs/features/0001-pr-review-pipeline.md),
  [ADR-0004](0004-provider-agnostic-explicit-pipeline.md)
