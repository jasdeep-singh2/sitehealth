# SiteHealth

AI-powered website health monitor: .NET, React, Azure and Claude. Built in public.

SiteHealth crawls a website, finds broken links, SEO problems, slow pages and accessibility issues, and uses Claude to explain each problem and suggest a fix. Results show up in a dashboard and, later, directly inside the Optimizely CMS editor.

> **Status:** Day 1, project setup. Follow along in the [Learning log](#learning-log).

## Why I'm building this

I'm a .NET and Optimizely developer. I'm building SiteHealth in public to level up in:

- **System design:** queues, workers, caching, retries, scaling
- **Azure:** building and running a real production workload, not just tutorials
- **AI engineering:** LLM integration, RAG, MCP and agents with Claude
- **React / Next.js:** a modern front end and headless Optimizely

Every Optimizely site needs to stay healthy, so this project is useful to me, to clients and to the community.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core minimal APIs, EF Core |
| Workers | .NET background services → Azure Container Apps |
| Messaging | Azure Service Bus |
| Data | Azure SQL, Azure Cache for Redis, Blob Storage |
| Front end | React + TypeScript, Next.js |
| AI | Claude (Anthropic API / Microsoft Foundry), Azure AI Search |
| Infrastructure | Bicep, GitHub Actions |
| Observability | Application Insights + OpenTelemetry |
| CMS integration | Optimizely CMS 12 add-on, Optimizely Graph |

## Roadmap

### Phase 1: Foundations and MVP
- [ ] CLI link checker (fetch a page, extract links, check status codes)
- [ ] Claude explains broken links and suggests fixes (structured JSON)
- [ ] ASP.NET Core API + background worker
- [ ] React dashboard
- [ ] Deploy to Azure (App Service / Static Web Apps, Azure SQL, Key Vault, Managed Identity)
- [ ] CI/CD with GitHub Actions

### Phase 2: System design and async architecture
- [ ] Crawl jobs via Azure Service Bus with retries and a dead-letter queue
- [ ] Workers that scale on queue length (Container Apps)
- [ ] Redis caching and Blob Storage for snapshots
- [ ] Polite crawling: rate limiting and back-pressure
- [ ] Distributed tracing with Application Insights
- [ ] AI alt-text generation (vision) and issue triage with a small model

### Phase 3: Production-grade and Optimizely
- [ ] Authentication and multi-tenancy
- [ ] Infrastructure as code with Bicep
- [ ] Load testing and published results
- [ ] "Ask your site" chat with RAG over crawled content
- [ ] SiteHealth MCP server in C#
- [ ] Optimizely CMS add-on (NuGet) and headless demo with Optimizely Graph

### Phase 4: Agents and polish
- [ ] Audit agent that investigates issues and proposes fixes for human approval
- [ ] Evals for AI output quality
- [ ] Cost dashboard (prompt caching, batch processing, model routing)
- [ ] Case study and demo video

## Getting started

_Coming soon, once the first runnable code lands._

## Learning log

| Day | What I did | What I learned | Next |
|---|---|---|---|
| 1 | Set up tools, API key and this repo | | Build the CLI link checker |

## Follow along

- LinkedIn: _add your link_
- Blog: _add your link_
