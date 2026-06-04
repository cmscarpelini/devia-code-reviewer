# Bounded Contexts (DDD)

The domain is divided into contexts with clear boundaries. Each context has its own
model and vocabulary; integration between them is explicit.

```mermaid
flowchart TB
    subgraph ingestion["Code Ingestion Context"]
        ws["GitHub Webhook"]
        pr["Pull Request / Diff"]
    end
    subgraph review["Review Context (core)"]
        rv["Review / Findings / Summary"]
        pipe["Pipeline (Semantic Kernel)"]
    end
    subgraph decision["Decision Context"]
        vd["Verdict / Justification"]
        fb["Feedback (false-positive)"]
    end
    subgraph identity["Identity & Access Context"]
        usr["Users / Roles / Orgs"]
    end
    subgraph insights["Insights Context"]
        met["Metrics / Reports"]
    end

    ingestion -->|PR + diff| review
    review -->|assessment| decision
    decision -->|verdict events| insights
    decision -->|feedback| review
    identity -.authorizes.-> review
    identity -.authorizes.-> decision
```

## Contexts

| Context | Responsibility | Core? |
|---------|----------------|-------|
| **Code Ingestion** | Receive webhooks, fetch PR diff/metadata from GitHub | Supporting |
| **Review** | Orchestrate the AI pipeline, produce summary and findings | 🟢 **Core domain** |
| **Decision** | Record human verdict and feedback; reflect it on GitHub | 🟢 **Core domain** |
| **Identity & Access** | Authentication (GitHub OAuth), roles, organizations (RBAC) | Generic |
| **Insights** | Metrics, trends, and quality reports | Supporting |

## Mapping to code

Each context tends to become a module/namespace within the `Domain` and
`Application` layers. In the MVP, supporting contexts can be simplified; the
modeling investment concentrates on the **core** (Review + Decision).

> The product's **competitive edge** is in the core (Review + Decision):
> assessment quality and the *human-in-the-loop* flow with learning.
