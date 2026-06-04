# SPEC-0002 — GitHub onboarding (authentication & repository connection)

- **Status:** Draft
- **Phase:** MVP
- **Bounded Context:** Identity & Access
- **Author:** DevIA Team
- **Date:** 2026-06-01

## 1. Goal

Let a user sign in with their GitHub account and connect repositories to the
platform, so that Pull Requests on those repositories start triggering reviews
([SPEC-0001](0001-pr-review-pipeline.md)).

## 2. Context / Problem

Nothing works until (a) users can authenticate and (b) the platform has access to
repositories and receives their webhooks. This spec establishes the entry point and
populates the core Identity & Access entities (`User`, `Organization`, `Repository`)
from the [data model](../../domain/data-model.md).

GitHub provides **two distinct mechanisms** — both are needed:

| Mechanism | Used for |
|-----------|----------|
| **OAuth (user login)** | Authenticating a person; mapping to our `User` |
| **GitHub App (installation)** | Repo access + receiving PR webhooks |

## 3. Actors and Scope

- **Actors:** Developer / Senior Developer (sign in), Administrator (install the App, connect repos).
- **In scope:** GitHub OAuth login, GitHub App installation, listing & activating
  repositories, registering the webhook, persisting Identity entities.
- **Out of scope:** full RBAC management UI (Phase 2), org billing, SSO providers
  other than GitHub.

## 4. Expected Behavior (User Stories)

> As a **Developer**, I want to sign in with my GitHub account, so that I don't
> manage a separate credential.

> As an **Administrator**, I want to install the GitHub App on my organization and
> choose which repositories are reviewed, so that only the intended repos trigger reviews.

## 5. Scenarios (Gherkin)

```gherkin
Scenario: First sign-in with GitHub
  Given a user without an account
  When they authenticate via GitHub OAuth
  Then a User record is created (or updated) from their GitHub profile
  And a session (JWT) is issued

Scenario: Install the GitHub App on an organization
  Given an administrator signed in
  When they install the GitHub App on a GitHub organization
  Then an Organization record is created
  And the selected repositories are stored as Repository records (is_active = true)
  And the webhook for pull_request events is active for those repositories

Scenario: Deactivate a repository
  Given a connected repository
  When the administrator deactivates it
  Then is_active becomes false
  And new PRs on it no longer trigger reviews
```

## 6. Acceptance Criteria

- [ ] GitHub OAuth login creates or updates a `User` from the GitHub profile.
- [ ] A session token (JWT) is issued and validated on subsequent API calls.
- [ ] Installing the GitHub App creates an `Organization` and the selected `Repository` records.
- [ ] Only `is_active = true` repositories trigger reviews (enforced upstream in SPEC-0001).
- [ ] GitHub App installation tokens/secrets are stored **encrypted** at rest.
- [ ] Deactivating a repository stops new reviews without deleting history.
- [ ] A minimal read-only screen lists connected repositories with an active/inactive toggle.
- [ ] Webhook signature secret is configured per installation and validated (SPEC-0001).

## 7. Business Rules

- A `Repository` must belong to an `Organization` and be active to be reviewed.
- Re-installing or re-authorizing updates existing records (idempotent by GitHub IDs).
- In the MVP, the installing user is assigned an `Admin` role; others default to
  `Developer` (full RBAC matures in Phase 2).

## 8. Contracts / Interfaces

- `GET /auth/github/login` → redirects to GitHub OAuth.
- `GET /auth/github/callback` → exchanges code, creates/updates `User`, issues JWT.
- `POST /webhooks/github` → also receives the `installation` / `installation_repositories` events.
- Identity entities per [data model](../../domain/data-model.md): `User`, `Organization`, `Repository`, `Membership`.

## 9. Technical Considerations

- Distinguish **OAuth tokens** (user identity) from **App installation tokens** (repo access); store separately, encrypt installation credentials.
- Installation tokens are short-lived → refresh on demand when calling the GitHub API.
- Map GitHub numeric IDs (`github_user_id`, `github_org_id`, `github_repo_id`) as the natural keys for idempotent upserts.
- Secrets (client secret, webhook secret, GitHub App private key) via secure configuration,
  never in source: **.NET user-secrets / env vars in dev, Azure Key Vault in prod**.
  Load the private key **once** and cache installation tokens to minimize Key Vault
  reads (cost is negligible at this volume — pennies/month — and caching keeps it so).

## 10. Test Strategy

| Layer | What to test | How |
|-------|--------------|-----|
| Unit | OAuth callback mapping → User, idempotent upsert by GitHub ID, role assignment, token encryption | GitHub API mocked |
| Integration | login flow → JWT → authorized call; installation event → Organization + Repository persisted | Testcontainers (Postgres) + GitHub stub |

> No LLM involved → no evals in this spec.

## 11. Resolved Decisions

- **Multi-org (Q1):** one org per account in the MVP **UI**; the schema stays
  multi-org ready (`Organization` + `Membership`). An org switcher is deferred to Phase 2.
- **GitHub App private key (Q2):** dev → .NET user-secrets / env vars; prod → Azure
  Key Vault. Key loaded once and installation tokens cached to minimize reads. Key
  Vault Standard has no fixed fee and costs ~pennies/month at this volume.
- **Connected repositories screen (Q3):** a minimal read-only list in our app with an
  active/inactive toggle (`is_active`). Repo *selection* stays on GitHub's installation UI.
