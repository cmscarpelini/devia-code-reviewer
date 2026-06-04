# C4 — Level 1: Context Diagram

Shows the system as a single box, its users, and external systems.

```mermaid
flowchart TB
    dev["👤 Developer<br/>PR author"]
    sr["🧑‍💼 Senior Developer<br/>reviewer/decider"]
    lead["🛠️ Tech Lead<br/>rules and metrics"]
    admin["⚙️ Administrator<br/>orgs and costs"]

    sys["🤖 DevIA Code Reviewer<br/>AI-powered DevOps Orchestration Platform"]

    gh["🐙 GitHub<br/>PRs, Webhooks, API"]
    llm["🧠 LLM Provider<br/>Azure OpenAI / OpenAI / Anthropic"]

    dev -->|opens / updates PR| gh
    gh -->|PR webhook| sys
    sys -->|posts assessment and status| gh
    sr -->|approves / rejects with justification| sys
    sr -->|marks false-positive / valid| sys
    lead -->|configures rules / views metrics| sys
    admin -->|manages orgs, users and budget| sys
    sys -->|sends diff for analysis| llm
    llm -->|findings + summary| sys
```

## Actors

| Actor | Description | Goal |
|-------|-------------|------|
| Developer | PR author | Get fast, actionable feedback |
| Senior Developer | Reviewer/decider | Approve/reject based on the AI assessment |
| Tech Lead | Technical manager | Define standards and track quality |
| Administrator | Platform manager | Manage orgs, access, and costs |

## External Systems

| System | Integration |
|--------|-------------|
| GitHub | GitHub App + Webhooks + REST API (diff, comments, status) |
| LLM Provider | Chat/completions API, abstracted by Semantic Kernel |
