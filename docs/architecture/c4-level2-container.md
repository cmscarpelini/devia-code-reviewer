# C4 — Level 2: Container Diagram

Details the applications (containers) that make up the system and their technologies.

```mermaid
flowchart TB
    subgraph client["Client"]
        fe["🌐 Frontend SPA<br/>Next.js + React + TypeScript"]
    end

    subgraph platform["DevIA Code Reviewer"]
        api["🔌 REST / Webhook API<br/>.NET 10 / ASP.NET Core"]
        queue["📨 Message Queue<br/>RabbitMQ"]
        worker["⚙️ Review Worker<br/>.NET 10 Worker Service"]
        orch["🧩 Review Orchestrator<br/>Semantic Kernel"]
        pg[("🐘 PostgreSQL<br/>users, repos, reviews,<br/>verdicts, audit")]
        mongo[("🍃 MongoDB<br/>diffs, prompts,<br/>review results")]
    end

    gh["🐙 GitHub"]
    llm["🧠 LLM Provider"]

    fe -->|HTTPS / JWT| api
    gh -->|PR webhook| api
    api -->|enqueue ReviewJob| queue
    api <-->|CRUD / EF Core| pg
    api -->|read result| mongo
    queue -->|deliver job| worker
    worker --> orch
    orch -->|prompts| llm
    llm -->|structured responses| orch
    worker -->|store raw result| mongo
    worker -->|update status and verdict| pg
    worker -->|post comment| gh
```

## Containers

| Container | Technology | Responsibility | Persistence |
|-----------|-----------|----------------|-------------|
| Frontend SPA | Next.js / React / TS | UI: review queue, assessment, verdict | — |
| API | ASP.NET Core (.NET 10) | REST + webhook; validates, enqueues, serves data | Postgres, Mongo |
| Queue | RabbitMQ | Decouples reception from processing | — |
| Review Worker | .NET Worker | Runs the async review pipeline | Postgres, Mongo |
| Orchestrator | Semantic Kernel | Chains review prompts and plugins | — |
| PostgreSQL | EF Core | Relational, auditable data | — |
| MongoDB | MongoDB.Driver | Large/flexible review documents | — |

## Related decisions

- Hybrid Postgres + Mongo database → [ADR-0003](adr/0003-database-strategy-postgres-mongodb.md)
- Async processing via queue → see [overview](../architecture/overview.md)
