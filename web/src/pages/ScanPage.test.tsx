import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import type { ScanResponse } from '../api/client'
import { POLL_INTERVAL_MS } from '../api/queries'
import { LINKS_SHOWN_AT_FIRST } from '../components/GroupCard'
import { aGroup, aScan, fakeApi, json, problem, renderWithProviders, SCAN_ID } from '../test/utils'

const completed = (overrides: Partial<ScanResponse['result'] & object> = {}) =>
  aScan({
    status: 'Completed',
    completedAt: '2026-10-25T10:00:15+00:00',
    result: { linksChecked: 47, linksSkipped: 0, brokenCount: 1, groups: [aGroup()], ...overrides },
  })

const openScanPage = () => renderWithProviders(<App />, { route: `/scans/${SCAN_ID}` })

describe('scan page', () => {
  it('keeps checking until the scan completes, then stops', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const answers = [aScan({ status: 'Running' }), completed()]
    const fetchMock = fakeApi(() => json(answers.shift() ?? completed()))
    openScanPage()

    expect(await screen.findByText('Checking every link on the page…')).toBeInTheDocument()

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS))
    expect(await screen.findByText('status 404')).toBeInTheDocument()
    const callsWhenDone = fetchMock.mock.calls.length

    // Finished scans don't change, so no more requests
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3))
    expect(fetchMock.mock.calls.length).toBe(callsWhenDone)
  })

  it('shows the summary and each group with its advice', async () => {
    fakeApi(() => json(completed()))
    openScanPage()

    expect(await screen.findByText('Links checked')).toBeInTheDocument()
    expect(screen.getByText('47')).toBeInTheDocument()
    expect(screen.getByText('high')).toBeInTheDocument()
    expect(screen.getByText('The page was removed.')).toBeInTheDocument()
    expect(screen.getByText('Add a redirect.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'https://example.com/old' })).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('shortens long groups until "Show all" is pressed', async () => {
    const links = Array.from({ length: 8 }, (_, i) => ({ url: `https://example.com/old-${i}`, statusCode: 404, error: null }))
    fakeApi(() => json(completed({ brokenCount: 8, groups: [aGroup({ links })] })))
    openScanPage()

    expect(await screen.findAllByRole('link', { name: /old-/ })).toHaveLength(LINKS_SHOWN_AT_FIRST)

    await userEvent.click(screen.getByRole('button', { name: 'Show all 8 links' }))
    expect(screen.getAllByRole('link', { name: /old-/ })).toHaveLength(8)
  })

  it('celebrates when nothing is broken', async () => {
    fakeApi(() => json(completed({ brokenCount: 0, groups: [] })))
    openScanPage()

    expect(await screen.findByText('No broken links found. Nice work!')).toBeInTheDocument()
  })

  it('says when AI advice is missing and when links were skipped', async () => {
    fakeApi(() => json(completed({ linksSkipped: 120, groups: [aGroup({ advice: null })] })))
    openScanPage()

    expect(await screen.findByText(/No AI explanations for this scan/)).toBeInTheDocument()
    expect(screen.getByText(/120 more links on this page weren't checked/)).toBeInTheDocument()
  })

  it('shows why a scan failed', async () => {
    fakeApi(() => json(aScan({ status: 'Failed', error: 'Blocked: localtest.me points to a private or internal address' })))
    openScanPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('The scan failed: Blocked: localtest.me')
  })

  it('says when a scan does not exist', async () => {
    fakeApi(() => problem(404, { title: 'Scan not found.' }))
    openScanPage()

    expect(await screen.findByRole('heading', { name: 'Scan not found' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument()
  })
})
