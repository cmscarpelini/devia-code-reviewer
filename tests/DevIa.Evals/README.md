# DevIa.Evals — AI-quality eval harness

The **AI-quality half** of our testing strategy ([ADR-0005](../../docs/architecture/adr/0005-evaluation-strategy-for-ai-review.md),
[testing-and-evals.md](../../docs/architecture/testing-and-evals.md)). It runs the **real review
pipeline** over a versioned golden dataset and measures whether it actually catches the bugs —
recall / precision / F1 / false-positive rate / severity accuracy — plus an optional
LLM-as-judge score for the executive summary.

This is **not** part of the per-PR test run (it calls a real LLM → cost). Run it on changes that
affect AI quality (prompts/models), or locally as a smoke check.

## Layout

```
tests/DevIa.Evals/
├── dataset/      # golden cases (versioned): <id>/{diff.patch, expected.json, meta.json}
├── Scoring/      # FindingMatcher + EvalMetrics (deterministic — unit-tested in DevIa.UnitTests)
├── Runner/       # EvalRunner, SummaryJudge, report writer, gates
└── reports/      # generated metric reports (git-ignored)
```

A case's `expected.json` is the ground truth; a **clean** case (`shouldHaveFindings: false`,
no defects) measures false positives.

## Running

```bash
# Offline smoke test — no LLM, a perfect oracle exercises the full path (should score 1.0):
dotnet run --project tests/DevIa.Evals -- --offline

# Real run against the configured LLM (set Llm:ApiKey first, see below):
dotnet run --project tests/DevIa.Evals -- --judge

# Multi-run (the LLM is non-deterministic): mean ± stddev + per-case stability over N runs.
# A delay between calls keeps free-tier providers (GitHub Models) under their rate limit.
dotnet run --project tests/DevIa.Evals -- --judge --runs 3 --delay-ms 800

# Cache LLM results per (prompt version, model, diff): the first run pays, re-runs are ~free.
# Use it while iterating on the matcher/dataset/report without changing the prompt or model.
dotnet run --project tests/DevIa.Evals -- --judge --cache

# With gates (CI): non-zero exit if recall/FP thresholds are violated.
dotnet run --project tests/DevIa.Evals -- --min-recall 0.7 --max-fp-rate 1.0
```

On `--runs > 1` the report is the **aggregate** (mean ± population stddev per metric, plus each
case's detection rate / clean rate). HTTP 429s are retried with exponential backoff regardless.

> **Cache vs. variance.** `--cache` makes every run identical (a cache hit replays one stored
> result), so it nullifies `--runs` variance — they serve opposite goals. Use `--cache` to iterate
> cheaply on the harness or compare prompt versions; drop it (and use `--runs N`) to measure the
> model's real spread. Bump `Llm:PromptVersion` whenever the prompt changes to invalidate the cache.

### Options

| Flag | Default | Meaning |
|------|---------|---------|
| `--offline` | off | Use the no-LLM oracle (plumbing smoke test); gates not enforced |
| `--judge` | off | Also score each summary with the LLM-as-judge (1–5) |
| `--cache` | off | Reuse LLM results per (prompt version, model, diff) across re-runs |
| `--runs <n>` | `1` | Repeat the dataset N times; report mean ± stddev + per-case stability |
| `--delay-ms <n>` | `0` | Pause between LLM calls (smooths bursts against rate limits) |
| `--dataset <path>` | `./dataset` | Dataset directory |
| `--reports <path>` | next to dataset | Where to write the JSON report |
| `--line-tolerance <n>` | `3` | Line distance allowed when matching a finding to a defect |
| `--min-recall <d>` | `0.7` | Gate: minimum recall (mean, on multi-run) |
| `--max-fp-rate <d>` | `1.0` | Gate: max false positives per clean PR (mean, on multi-run) |

### LLM config

Uses the same `Llm` section as the Worker (GitHub Models by default). Provide the key out of
source control:

```bash
dotnet user-secrets set "Llm:ApiKey" "<token>" --project tests/DevIa.Evals
# or:  $env:Llm__ApiKey = "<token>"
```

## Adding a case

Every production miss / false positive should become a new case. Create
`dataset/<id>-<slug>/` with `diff.patch`, `expected.json` (labeled defects), and `meta.json`.
Keep clean PRs in the mix so precision/FP stay meaningful.
