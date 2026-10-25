import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { App } from '../App'
import { loadRecentScans } from '../recentScans'
import { aScan, fakeApi, json, problem, renderWithProviders, SCAN_ID } from '../test/utils'

describe('home page', () => {
  it('starts a scan, remembers it and opens its page', async () => {
    const fetchMock = fakeApi((request) =>
      request.method === 'POST' ? json(aScan(), { status: 202 }) : json(aScan({ status: 'Running' })),
    )
    renderWithProviders(<App />)

    await userEvent.type(screen.getByLabelText('Page to check'), 'example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Scan' }))

    // The scan page has opened and is showing progress
    expect(await screen.findByText('Checking every link on the page…')).toBeInTheDocument()
    expect(await fetchMock.mock.calls[0][0].json()).toEqual({ url: 'https://example.com' })
    expect(loadRecentScans()[0]).toMatchObject({ id: SCAN_ID, url: 'https://example.com/' })
  })

  it('shows the API’s reason when a url is refused', async () => {
    fakeApi(() => problem(400, { errors: { url: ['SiteHealth can only scan public websites.'] } }))
    renderWithProviders(<App />)

    await userEvent.type(screen.getByLabelText('Page to check'), 'http://localhost')
    await userEvent.click(screen.getByRole('button', { name: 'Scan' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('SiteHealth can only scan public websites.')
    expect(screen.getByLabelText('Page to check')).toHaveAttribute('aria-invalid', 'true')
  })

  it('lists recent scans from this browser', async () => {
    localStorage.setItem(
      'sitehealth.recentScans',
      JSON.stringify([{ id: SCAN_ID, url: 'https://example.com/', createdAt: '2026-10-25T10:00:00Z' }]),
    )
    renderWithProviders(<App />)

    expect(screen.getByRole('link', { name: 'https://example.com/' })).toHaveAttribute('href', `/scans/${SCAN_ID}`)
  })

  it('keeps the Scan button disabled until a url is typed', () => {
    renderWithProviders(<App />)

    expect(screen.getByRole('button', { name: 'Scan' })).toBeDisabled()
  })
})
