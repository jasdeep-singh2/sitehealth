# SiteHealth web dashboard

The React front end for SiteHealth: start a scan, watch its progress, and read the broken links grouped by
problem with Claude's advice.

Built with Vite, React, TypeScript, TanStack Query and React Router. Tests use Vitest and React Testing Library.

## Run it

You need the API running too (from the repository root):

```bash
dotnet run --project src/SiteHealth.Api
```

Then, in this folder:

```bash
npm install
npm run dev
```

Open http://localhost:5173. Vite forwards `/api/*` to the API on http://localhost:5080.

## Scripts

| Script | What it does |
|---|---|
| `npm run dev` | Start the dev server with hot reload |
| `npm test` | Run the tests once (`npm run test:watch` to keep them running) |
| `npm run lint` | Check the code with oxlint |
| `npm run build` | Type-check and build for production into `dist/` |
| `npm run generate:api` | Regenerate `src/api/schema.d.ts` from the API's `openapi.json` |

## API types

`src/api/schema.d.ts` is generated from `src/SiteHealth.Api/openapi/openapi.json`, which the .NET build
writes. After changing the API, build it, then run `npm run generate:api` and commit both files.
CI fails if either is out of date.
