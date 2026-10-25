import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import type { BrokenLinkGroup, ScanResponse } from '../api/client'

/** Renders a component with everything the app normally provides: a router at `route`, and a fresh cache. */
export function renderWithProviders(ui: ReactElement, { route = '/' } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  )
}

/**
 * Replaces fetch with a fake API, like the C# tests replace HttpClient's handler.
 * The handler gets each Request and decides the Response. Returns the mock, to count or inspect calls.
 */
export function fakeApi(handler: (request: Request) => Response | Promise<Response>) {
  const fetchMock = vi.fn(async (request: Request) => handler(request))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init.headers },
  })

export const problem = (status: number, body: Record<string, unknown>, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify({ status, ...body }), {
    status,
    headers: { 'Content-Type': 'application/problem+json', ...headers },
  })

export const SCAN_ID = '01a1729a-a38e-7634-91f6-277acd9987a8'

export function aScan(overrides: Partial<ScanResponse> = {}): ScanResponse {
  return {
    id: SCAN_ID,
    url: 'https://example.com/',
    status: 'Queued',
    createdAt: '2026-10-25T10:00:00+00:00',
    completedAt: null,
    error: null,
    result: null,
    ...overrides,
  }
}

export function aGroup(overrides: Partial<BrokenLinkGroup> = {}): BrokenLinkGroup {
  return {
    id: 1,
    isSameSite: true,
    problem: 'status 404',
    advice: { severity: 'high', cause: 'The page was removed.', fix: 'Add a redirect.' },
    links: [{ url: 'https://example.com/old', statusCode: 404, error: null }],
    ...overrides,
  }
}
