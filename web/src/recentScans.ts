/**
 * The scans you started from this browser, so you can get back to them.
 *
 * Kept in localStorage on purpose: the API has no "list all scans" endpoint, because without sign-in
 * it would show everyone which sites other people scanned. Once there are user accounts this can
 * become a real list from the API.
 */
export interface RecentScan {
  id: string
  url: string
  createdAt: string
}

const STORAGE_KEY = 'sitehealth.recentScans'
export const MAX_RECENT_SCANS = 10

export function loadRecentScans(): RecentScan[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter(isRecentScan).slice(0, MAX_RECENT_SCANS) : []
  } catch {
    // Storage blocked (private mode, browser settings) or the value was damaged: just show nothing
    return []
  }
}

export function rememberScan(scan: RecentScan): void {
  try {
    const others = loadRecentScans().filter((s) => s.id !== scan.id)
    localStorage.setItem(STORAGE_KEY, JSON.stringify([scan, ...others].slice(0, MAX_RECENT_SCANS)))
  } catch {
    // Not being able to remember a scan isn't worth interrupting anyone for
  }
}

function isRecentScan(value: unknown): value is RecentScan {
  const scan = value as RecentScan
  return typeof scan?.id === 'string' && typeof scan.url === 'string' && typeof scan.createdAt === 'string'
}
