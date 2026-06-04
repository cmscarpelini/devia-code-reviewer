# ADR-0004 — Provider-agnostic, explicitly-orchestrated review pipeline

- **Status:** Accepted
- **Date:** 2026-06-01

## Context

The review pipeline runs LLM calls via Semantic Kernel. Two design questions arise:

1. **Orchestration:** let an SK *planner* decide the steps at runtime, or invoke a
   **fixed sequence** of steps in code?
2. **Provider coupling:** bind the pipeline to a concrete LLM connector (e.g., Azure
   OpenAI), or keep it abstract?

Constraints: the MVP develops for free (GitHub Models / Ollama) but runs on Azure
OpenAI in production, and Phase 3 introduces multi-LLM. Reviews must be testable
without incurring LLM cost, and behavior must be predictable and cheap.

## Decision

1. **Explicit orchestration.** The pipeline is a fixed, code-defined sequence
   (fetch → redact → chunk → summarize → analyze → consolidate → validate →
   persist). We do **not** use an SK auto-planner to choose steps.
2. **Provider-agnostic core.** The orchestrator depends only on SK's
   `IChatCompletionService` / `Kernel`. The concrete connector is selected by
   **configuration** in a single Infrastructure seam (`KernelSetup`). The pipeline
   code never references a specific provider.
3. **Portable structured output.** Baseline is "request JSON + validate + repair";
   native provider structured-output is an optional optimization, never a dependency.

## Consequences

**Positive**
- Deterministic, predictable cost and latency; easy to reason about.
- Unit-testable by mocking `IChatCompletionService` (zero LLM cost).
- Swapping dev (GitHub Models/Ollama) ↔ prod (Azure OpenAI) is config-only.
- Phase 3 multi-LLM adds connectors without touching pipeline code.

**Negative / costs**
- We hand-write the orchestration instead of delegating to a planner.
- Portable JSON handling needs an explicit validator + repair step.

## References

- Design detail: [review-pipeline.md](../review-pipeline.md)
- Related: [SPEC-0001](../../specs/features/0001-pr-review-pipeline.md)
