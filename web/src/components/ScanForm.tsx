import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { normalizeUrl } from '../api/client'
import { useCreateScan } from '../api/queries'
import { rememberScan } from '../recentScans'

/** "Enter a URL, press Scan": starts a scan and opens its page. */
export function ScanForm() {
  const [url, setUrl] = useState('')
  const createScan = useCreateScan()
  const navigate = useNavigate()

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!url.trim()) return

    createScan.mutate(normalizeUrl(url), {
      onSuccess: (scan) => {
        rememberScan({ id: scan.id, url: scan.url, createdAt: scan.createdAt })
        navigate(`/scans/${scan.id}`)
      },
    })
  }

  return (
    <form className="scan-form" onSubmit={handleSubmit} noValidate>
      <label htmlFor="scan-url">Page to check</label>
      <div className="scan-form__row">
        {/* type="text" rather than "url": the browser's own check would reject "example.com",
            which we happily turn into https://example.com */}
        <input
          id="scan-url"
          type="text"
          inputMode="url"
          autoComplete="url"
          placeholder="https://example.com"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          aria-invalid={createScan.isError}
          aria-describedby={createScan.isError ? 'scan-url-error' : undefined}
        />
        <button type="submit" disabled={createScan.isPending || !url.trim()}>
          {createScan.isPending ? 'Starting…' : 'Scan'}
        </button>
      </div>
      {createScan.isError && (
        <p id="scan-url-error" className="form-error" role="alert">
          {createScan.error.message}
        </p>
      )}
    </form>
  )
}
