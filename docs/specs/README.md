# Specs — Feature Specifications

Following [Spec-Driven Development](../methodology.md), every feature starts as a
spec **before** it becomes code. The spec is the source of truth.

## Structure

| Folder | Contents |
|--------|----------|
| [`templates/`](templates/) | Standard template for new specs |
| [`features/`](features/) | Feature specs, numbered |

## Conventions

- File name: `NNNN-title-in-kebab-case.md` (sequential numbering).
- Start from the [template](templates/feature-spec-template.md).
- Every spec has **verifiable acceptance criteria** (they become tests/evals).
- Behavior changed? Update the spec **together** with the code (in the same PR).

## Spec Index

| # | Feature | Phase | Status |
|---|---------|-------|--------|
| [0001](features/0001-pr-review-pipeline.md) | PR review pipeline (webhook → assessment) | MVP | 📝 Draft |
| [0002](features/0002-github-onboarding.md) | GitHub onboarding (auth & repo connection) | MVP | 📝 Draft |
| [0003](features/0003-human-verdict.md) | Human verdict (approve/reject) | MVP | 📝 Draft |
| [0004](features/0004-review-dashboard.md) | Review dashboard (queue & assessment view) | MVP | 📝 Draft |

## Workflow

```
1. Write spec (Draft)         →  features/NNNN-*.md
2. Review and approve (Ready) →  team validates acceptance criteria
3. Implement                  →  code guided by the spec
4. Verify                     →  tests (deterministic) + evals (AI quality)
5. Mark as Done               →  spec reflects what was delivered
```
