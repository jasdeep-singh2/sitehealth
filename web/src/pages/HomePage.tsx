import { RecentScans } from '../components/RecentScans'
import { ScanForm } from '../components/ScanForm'

export function HomePage() {
  return (
    <>
      <section className="panel">
        <h1>Check a page for broken links</h1>
        <p className="muted">
          SiteHealth checks every link on the page, groups the broken ones by problem and, when AI is switched
          on, explains each problem with a suggested fix.
        </p>
        <ScanForm />
      </section>

      <section className="panel">
        <h2>Your recent scans</h2>
        <RecentScans />
      </section>
    </>
  )
}
