import { describe, expect, it } from 'vitest'
import { aScan, fakeApi, json, problem, SCAN_ID } from '../test/utils'
import { ApiError, createScan, getScan, normalizeUrl } from './client'

describe('normalizeUrl', () => {
  it('adds https:// when there is no scheme', () => {
    expect(normalizeUrl('  example.com/blog ')).toBe('https://example.com/blog')
  })

  it('keeps a scheme that is already there', () => {
    expect(normalizeUrl('http://example.com')).toBe('http://example.com')
    expect(normalizeUrl('HTTPS://Example.com')).toBe('HTTPS://Example.com')
  })
})

describe('createScan', () => {
  it('posts the url to /api/scans and returns the new scan', async () => {
    const fetchMock = fakeApi(() => json(aScan(), { status: 202 }))

    const scan = await createScan('https://example.com/')

    expect(scan.id).toBe(SCAN_ID)
    const request = fetchMock.mock.calls[0][0]
    expect(request.method).toBe('POST')
    expect(new URL(request.url).pathname).toBe('/api/scans')
    expect(await request.json()).toEqual({ url: 'https://example.com/' })
  })

  it('uses the validation message from the API', async () => {
    fakeApi(() =>
      problem(400, {
        title: 'One or more validation errors occurred.',
        errors: { url: ['SiteHealth can only scan public websites.'] },
      }),
    )

    await expect(createScan('http://localhost')).rejects.toMatchObject({
      message: 'SiteHealth can only scan public websites.',
      status: 400,
    })
  })

  it('says how long to wait when rate-limited', async () => {
    fakeApi(() => problem(429, { title: 'Too many requests.' }, { 'Retry-After': '30' }))

    const error = await createScan('https://example.com/').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 429, retryAfterSeconds: 30 })
    expect((error as ApiError).message).toContain('in 30 seconds')
  })

  it('explains when the API cannot be reached', async () => {
    fakeApi(() => {
      throw new TypeError('Failed to fetch')
    })

    await expect(createScan('https://example.com/')).rejects.toMatchObject({ status: 0 })
  })
})

describe('getScan', () => {
  it('gets the scan by id', async () => {
    const fetchMock = fakeApi(() => json(aScan({ status: 'Running' })))

    expect((await getScan(SCAN_ID)).status).toBe('Running')
    expect(new URL(fetchMock.mock.calls[0][0].url).pathname).toBe(`/api/scans/${SCAN_ID}`)
  })

  it('turns 404 into a "not found" error', async () => {
    fakeApi(() => problem(404, { title: 'Scan not found.' }))

    await expect(getScan(SCAN_ID)).rejects.toMatchObject({ status: 404, message: "We couldn't find that scan." })
  })
})
