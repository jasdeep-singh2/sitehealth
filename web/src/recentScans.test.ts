import { describe, expect, it } from 'vitest'
import { loadRecentScans, MAX_RECENT_SCANS, rememberScan } from './recentScans'

const scan = (n: number) => ({ id: `id-${n}`, url: `https://example.com/${n}`, createdAt: '2026-10-25T10:00:00Z' })

describe('recent scans', () => {
  it('starts empty', () => {
    expect(loadRecentScans()).toEqual([])
  })

  it('keeps the newest first and does not repeat a scan', () => {
    rememberScan(scan(1))
    rememberScan(scan(2))
    rememberScan(scan(1))

    expect(loadRecentScans().map((s) => s.id)).toEqual(['id-1', 'id-2'])
  })

  it(`keeps at most ${MAX_RECENT_SCANS}`, () => {
    for (let n = 1; n <= MAX_RECENT_SCANS + 3; n++) rememberScan(scan(n))

    const recent = loadRecentScans()
    expect(recent).toHaveLength(MAX_RECENT_SCANS)
    expect(recent[0].id).toBe(`id-${MAX_RECENT_SCANS + 3}`)
  })

  it('ignores damaged storage instead of crashing', () => {
    localStorage.setItem('sitehealth.recentScans', '{not json')
    expect(loadRecentScans()).toEqual([])

    localStorage.setItem('sitehealth.recentScans', JSON.stringify([{ id: 1 }, scan(5)]))
    expect(loadRecentScans().map((s) => s.id)).toEqual(['id-5'])
  })
})
