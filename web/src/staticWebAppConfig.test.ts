import { describe, expect, it } from 'vitest'
import config from '../public/staticwebapp.config.json'

// Azure Static Web Apps only serves files that exist. Opening /scans/{id} directly (a bookmark, a refresh)
// must fall back to index.html so React Router can show the page, while real files keep working.

const isExcluded = (path: string) =>
  config.navigationFallback.exclude.some((pattern) =>
    new RegExp(`^${pattern.replace(/[.]/g, '\\.').replace(/\*/g, '.*')}$`).test(path),
  )

describe('staticwebapp.config.json', () => {
  it('serves the app for its own routes', () => {
    expect(config.navigationFallback.rewrite).toBe('/index.html')
    expect(isExcluded('/scans/01a1729a-a38e-7634-91f6-277acd9987a8')).toBe(false)
    expect(isExcluded('/')).toBe(false)
  })

  it('does not rewrite real files', () => {
    expect(isExcluded('/assets/index-abc123.js')).toBe(true)
    expect(isExcluded('/favicon.svg')).toBe(true)
  })

  it('sends basic security headers', () => {
    expect(config.globalHeaders['X-Content-Type-Options']).toBe('nosniff')
    expect(config.globalHeaders['X-Frame-Options']).toBe('DENY')
  })
})
