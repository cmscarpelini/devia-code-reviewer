# SPEC-NNNN — <Feature Title>

- **Status:** Draft | Ready | In Progress | Done
- **Phase:** MVP | Increment | Scale
- **Bounded Context:** <e.g., Review Context>
- **Author:** <name>
- **Date:** <YYYY-MM-DD>

## 1. Goal

<One or two sentences: what this feature delivers and for whom.>

## 2. Context / Problem

<Why this is needed. What pain it solves. Reference ADRs or other specs.>

## 3. Actors and Scope

- **Actors involved:** <e.g., Developer, Senior Developer>
- **In scope:** <list>
- **Out of scope:** <list — being explicit avoids wrong expectations>

## 4. Expected Behavior (User Stories)

> As a **\<actor\>**, I want **\<action\>**, so that **\<benefit\>**.

## 5. Scenarios (Gherkin)

```gherkin
Scenario: <name>
  Given <precondition>
  When <action>
  Then <expected result>
```

## 6. Acceptance Criteria

<A verifiable list. Each item should be able to become a test or eval.>

- [ ] ...
- [ ] ...

## 7. Business Rules

<Invariants, validations, severities, applicable gates.>

## 8. Contracts / Interfaces

<Endpoints, payloads, events, expected LLM output schema (if applicable).>

## 9. Technical Considerations

<Architecture, persistence, idempotency, performance, security points.>

## 10. Test Strategy

| Layer | What to test | How |
|-------|--------------|-----|
| Unit | <deterministic logic> | LLM mocked |
| Integration | <end-to-end flow> | test environment |
| Eval (if AI involved) | <output quality> | golden dataset + metrics |

## 11. Open Questions

<Questions to resolve before "Ready".>
