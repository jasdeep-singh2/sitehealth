# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

SiteHealth is an AI-powered website health monitor: crawl a site, detect broken links, SEO, performance and accessibility issues, and use Claude to explain them and suggest fixes. See README.md for the roadmap.

This is a **learning project built in public**. The owner is a .NET/Optimizely developer learning system design, Azure and AI engineering.

## How to work with me

- **Write the code for me.** Write the full implementation yourself. No fill-in-the-blank exercises or "you write this" TODOs.
- **Then explain what you did and why.** After each change, walk me through the key decisions in plain language, and explain any new concept or pattern you used.
- **Plan first** for anything bigger than a small fix: propose the approach and trade-offs before writing code.
- Keep changes small and focused, one concern per commit.

## Stack

- .NET 10 (C#, nullable enabled), ASP.NET Core minimal APIs, EF Core
- React + TypeScript, later Next.js
- Azure: App Service / Container Apps, Service Bus, Azure SQL, Redis, Blob Storage, Key Vault, Application Insights
- Claude via the Anthropic Messages API
- Bicep for infrastructure, GitHub Actions for CI/CD

## Conventions

- Use `HttpClient` through `IHttpClientFactory`. Never `new HttpClient()` per request.
- Async all the way, and pass `CancellationToken` through.
- Prefer `record` types for DTOs and AI response models.
- Configuration through the Options pattern. No hard-coded URLs or settings.
- Tests with xUnit. Add a test with every new behaviour.

## Secrets: never commit them

- The Anthropic API key is read from the `ANTHROPIC_API_KEY` environment variable.
- Local secrets go in `dotnet user-secrets`, Azure secrets in Key Vault with Managed Identity.
- Never print, log or hard-code keys, tokens or connection strings.

## AI usage

- Default model: `claude-sonnet-5-5`. Use `claude-haiku-5-5` for cheap, high-volume tasks such as classification.
- Ask Claude for structured JSON output and validate it before use.
- Keep prompts in their own files or constants, not scattered through the code.

## Crawling etiquette

- Respect `robots.txt`, limit concurrency and add delays between requests.
- Only crawl sites the user owns or is allowed to test.

## Commands

_Fill in as the solution grows:_

- Build: `dotnet build`
- Test: `dotnet test`
- Run CLI: `dotnet run --project src/SiteHealth.Cli -- <url>`
- Run API: `dotnet run --project src/SiteHealth.Api` (http://localhost:5080, Scalar at /scalar, or `src/SiteHealth.Api/SiteHealth.Api.http`)
- Run dashboard: `cd web && npm run dev` (http://localhost:5173; forwards /api to the API)
- Web tests and lint: `cd web && npm test && npm run lint`
- After changing the API's contract: build, then `cd web && npm run generate:api`, and commit `openapi.json` and `schema.d.ts`
