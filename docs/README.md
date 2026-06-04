# Documentation — DevIA Code Reviewer

Central documentation index. Documentation is organized according to the adopted
methodology (see [methodology.md](methodology.md)): **Spec-Driven + DDD + C4 + ADR**.

## Documentation Map

| Area | File | Purpose |
|------|------|---------|
| 🧭 Methodology | [methodology.md](methodology.md) | How we work and why |
| 🗺️ Architecture — overview | [architecture/overview.md](architecture/overview.md) | Architectural summary and code structure |
| 🗺️ C4 Level 1 — Context | [architecture/c4-level1-context.md](architecture/c4-level1-context.md) | System and external actors |
| 🗺️ C4 Level 2 — Container | [architecture/c4-level2-container.md](architecture/c4-level2-container.md) | Applications and technologies |
| 🗺️ C4 Level 3 — Review Pipeline | [architecture/review-pipeline.md](architecture/review-pipeline.md) | Semantic Kernel orchestration (provider-agnostic) |
| 🧪 Testing & Evals | [architecture/testing-and-evals.md](architecture/testing-and-evals.md) | Deterministic tests + AI-quality evals |
| 📌 Decisions (ADR) | [architecture/adr/README.md](architecture/adr/README.md) | Architecture decision records |
| 🧠 Ubiquitous Language | [domain/ubiquitous-language.md](domain/ubiquitous-language.md) | Shared domain vocabulary |
| 🧠 Bounded Contexts | [domain/bounded-contexts.md](domain/bounded-contexts.md) | Domain boundaries (DDD) |
| 🗄️ Data Model | [domain/data-model.md](domain/data-model.md) | Relational + document model (Postgres + Mongo) |
| 📝 Specs | [specs/README.md](specs/README.md) | Feature specifications |

## Getting Started (development)

1. Read the [methodology](methodology.md) and the [architecture overview](architecture/overview.md).
2. Get familiar with the [ubiquitous language](domain/ubiquitous-language.md).
3. Pick a spec in [`specs/features/`](specs/features/) (or write a new one from the [template](specs/templates/feature-spec-template.md)).
4. Implement following the spec; record relevant decisions as an [ADR](architecture/adr/README.md).

## Roadmap (phases)

| Phase | Goal | Status |
|-------|------|--------|
| **0 — Foundation** | Documentation, architecture, MVP specs | ✅ Done |
| **1 — MVP** | PR → AI review → human verdict | 🚧 In progress (solution scaffolded) |
| **2 — Increments** | Feedback loop, per-repo rules, metrics, RBAC | ⬜ Planned |
| **3 — Scale** | Multi-tenant, repo RAG, multi-LLM, auto-fix | ⬜ Planned |
