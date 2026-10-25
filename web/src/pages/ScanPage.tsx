import { Link, useParams } from 'react-router'
import { ApiError, type ScanResponse, type ScanResult } from '../api/client'
import { useScan } from '../api/queries'
import { GroupCard } from '../components/GroupCard'
import { StatusBadge } from '../components/StatusBadge'
import { formatDateTime, plural } from '../format'

/** One scan: its progress while it runs, then what it found. */
export function ScanPage() {
  const { id = '' } = useParams()
  const { data: scan, error, isPending, refetch } = useScan(id)

  if (isPending) {
    return <p className="panel muted">Loading scan…</p>
  }

  if (!scan) {
    return (
      <section className="panel">
        <h1>{error instanceof ApiError && error.status === 404 ? 'Scan not found' : "Couldn't load this scan"}</h1>
        <p role="alert">{error?.message}</p>
        <p className="actions">
          {!(error instanceof ApiError && error.status === 404) && (
            <button type="button" onClick={() => refetch()}>
              Try again
            </button>
          )}
          <Link to="/">Start a new scan</Link>
        </p>
      </section>
    )
  }

  return (
    <>
      <section className="panel">
        <p className="muted">
          <Link to="/">← New scan</Link>
        </p>
        <h1 className="scan-title">
          <a href={scan.url} target="_blank" rel="noopener noreferrer">
            {scan.url}
          </a>
        </h1>
        <p className="scan-meta">
          <StatusBadge status={scan.status} />
          <span className="muted">Started {formatDateTime(scan.createdAt)}</span>
        </p>
        <Progress scan={scan} />
      </section>

      {scan.status === 'Completed' && scan.result && <Results result={scan.result} />}
    </>
  )
}

function Progress({ scan }: { scan: ScanResponse }) {
  // aria-live: screen readers announce the change from "waiting" to "checking" to done
  return (
    <div aria-live="polite">
      {scan.status === 'Queued' && <p className="progress">Waiting for its turn…</p>}
      {scan.status === 'Running' && <p className="progress">Checking every link on the page…</p>}
      {scan.status === 'Failed' && (
        <p className="form-error" role="alert">
          The scan failed: {scan.error ?? 'unknown error'}
        </p>
      )}
    </div>
  )
}

function Results({ result }: { result: ScanResult }) {
  const hasAdvice = result.groups.some((group) => group.advice)

  return (
    <section className="panel">
      <div className="summary">
        <Stat label="Links checked" value={result.linksChecked} />
        <Stat label="Broken" value={result.brokenCount} tone={result.brokenCount > 0 ? 'bad' : 'good'} />
        <Stat label="Problems" value={result.groups.length} />
      </div>

      {result.linksSkipped > 0 && (
        <p className="note">
          {plural(result.linksSkipped, 'more link')} on this page weren't checked, because there's a limit per page.
        </p>
      )}

      {result.brokenCount === 0 ? (
        <p className="good-news">No broken links found. Nice work!</p>
      ) : (
        <>
          {!hasAdvice && (
            <p className="note">No AI explanations for this scan. They appear when the API has an Anthropic API key.</p>
          )}
          <h2>Broken links, grouped by problem</h2>
          <div className="groups">
            {result.groups.map((group) => (
              <GroupCard key={group.id} group={group} />
            ))}
          </div>
        </>
      )}
    </section>
  )
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: 'good' | 'bad' }) {
  return (
    <div className={`stat${tone ? ` stat--${tone}` : ''}`}>
      <span className="stat__value">{value}</span>
      <span className="stat__label">{label}</span>
    </div>
  )
}
