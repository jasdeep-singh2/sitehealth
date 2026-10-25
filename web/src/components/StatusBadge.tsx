import type { ScanStatus } from '../api/client'

const labels: Record<ScanStatus, string> = {
  Queued: 'Queued',
  Running: 'Running',
  Completed: 'Completed',
  Failed: 'Failed',
}

export function StatusBadge({ status }: { status: ScanStatus }) {
  return <span className={`badge badge--${status.toLowerCase()}`}>{labels[status]}</span>
}
