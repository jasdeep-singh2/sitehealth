import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

// Friendly names for the generated types. schema.d.ts is generated from the API's openapi.json
// (npm run generate:api), so these always match the C# records.
export type ScanResponse = components['schemas']['ScanResponse']
export type ScanStatus = components['schemas']['ScanStatus']
export type ScanResult = components['schemas']['ScanResult']
export type BrokenLinkGroup = components['schemas']['BrokenLinkGroup']
export type BrokenLink = components['schemas']['BrokenLink']

/** Where the API lives. In development Vite forwards /api to it (see vite.config.ts). */
const baseUrl = new URL(import.meta.env.VITE_API_BASE_URL ?? '/api', globalThis.location?.origin).toString()

// openapi-fetch checks every path, parameter and body against the generated types at compile time.
// fetch is looked up on each call (not captured once), so tests can swap in a fake.
const client = createClient<paths>({ baseUrl, fetch: (request) => globalThis.fetch(request) })

/** An error the UI can show as is, with extra detail when the API sent it. */
export class ApiError extends Error {
  readonly status: number
  readonly retryAfterSeconds?: number

  constructor(message: string, status: number, retryAfterSeconds?: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.retryAfterSeconds = retryAfterSeconds
  }
}

const unreachable = () =>
  new ApiError("Couldn't reach the SiteHealth API. Check that it's running and try again.", 0)

// The API answers errors with ProblemDetails: { title, errors?: { url: ["..."] } }
type Problem = { title?: string | null; errors?: Record<string, string[]> }

function toApiError(response: Response, problem: Problem | undefined): ApiError {
  if (response.status === 429) {
    const seconds = Number(response.headers.get('Retry-After')) || undefined
    const wait = seconds ? `in ${seconds} seconds` : 'in a minute'
    return new ApiError(`You're going a bit fast. Try again ${wait}.`, 429, seconds)
  }

  const fieldError = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined
  return new ApiError(fieldError ?? problem?.title ?? `Something went wrong (HTTP ${response.status}).`, response.status)
}

/** Starts a scan. Resolves with the new scan (status Queued), or throws an ApiError. */
export async function createScan(url: string): Promise<ScanResponse> {
  const result = await client.POST('/scans', { body: { url } }).catch(() => {
    throw unreachable()
  })

  if (!result.data) throw toApiError(result.response, result.error as Problem | undefined)
  return result.data
}

/** Gets a scan's current status and, once it's completed, its result. Throws an ApiError (404 if unknown). */
export async function getScan(id: string): Promise<ScanResponse> {
  const result = await client.GET('/scans/{id}', { params: { path: { id } } }).catch(() => {
    throw unreachable()
  })

  if (!result.data) {
    if (result.response.status === 404) throw new ApiError("We couldn't find that scan.", 404)
    throw toApiError(result.response, result.error as Problem | undefined)
  }
  return result.data
}

export const isFinished = (status: ScanStatus | undefined) => status === 'Completed' || status === 'Failed'

/** "example.com" is what people type; the API needs "https://example.com". */
export function normalizeUrl(input: string): string {
  const trimmed = input.trim()
  return /^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`
}
