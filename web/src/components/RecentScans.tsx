import { Link } from 'react-router'
import { loadRecentScans } from '../recentScans'
import { formatDateTime } from '../format'

/** The scans started from this browser, newest first. */
export function RecentScans() {
  const scans = loadRecentScans()

  if (scans.length === 0) {
    return <p className="muted">Scans you start will appear here, so you can come back to them.</p>
  }

  return (
    <ul className="recent-scans">
      {scans.map((scan) => (
        <li key={scan.id}>
          <Link to={`/scans/${scan.id}`}>{scan.url}</Link>
          <span className="muted">{formatDateTime(scan.createdAt)}</span>
        </li>
      ))}
    </ul>
  )
}
