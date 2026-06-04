# DevIA Web (Dashboard)

Next.js (App Router) dashboard for the DevIA Code Reviewer — the reviewer's workspace
(SPEC-0004): review queue, assessment detail, and the approve/reject action.

## Pages

| Route | Purpose |
|-------|---------|
| `/login` | Sign in with GitHub (or paste a dev token) |
| `/reviews` | Queue of PRs awaiting a verdict |
| `/reviews/[id]` | Assessment (summary + findings) + verdict action |

## Run

```bash
npm install
npm run dev        # http://localhost:3000
```

The API base URL defaults to `http://localhost:5080`. Override with an env var:

```bash
# .env.local
NEXT_PUBLIC_API_URL=http://localhost:5080
```

For the GitHub sign-in button to complete the round-trip, set the API's
`Auth:FrontendLoginUrl` to `http://localhost:3000/login` so the OAuth callback
redirects back with the token.

## Auth

The token (JWT) is stored in `localStorage`. Requests send it as `Authorization: Bearer`.
Only authenticated users can read the queue; only the Reviewer/Admin role can record a verdict.
