import { Link, Route, Routes } from 'react-router'
import { HomePage } from './pages/HomePage'
import { ScanPage } from './pages/ScanPage'

export function App() {
  return (
    <div className="app">
      <header className="site-header">
        <Link to="/" className="brand">
          <span className="brand__mark" aria-hidden="true" />
          SiteHealth
        </Link>
      </header>

      <main className="content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/scans/:id" element={<ScanPage />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>

      <footer className="site-footer muted">
        Only scan sites you own or are allowed to test.
      </footer>
    </div>
  )
}

function NotFound() {
  return (
    <section className="panel">
      <h1>Page not found</h1>
      <Link to="/">Go to the start page</Link>
    </section>
  )
}
