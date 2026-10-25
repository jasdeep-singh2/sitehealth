import { useState } from 'react'
import type { BrokenLink, BrokenLinkGroup } from '../api/client'
import { plural } from '../format'

/** How many links a group shows before "Show all". Big groups (38 × 404) would bury everything below. */
export const LINKS_SHOWN_AT_FIRST = 5

/** One group of broken links: what's wrong, Claude's advice, and the links themselves. */
export function GroupCard({ group }: { group: BrokenLinkGroup }) {
  const [showAll, setShowAll] = useState(false)
  const links = showAll ? group.links : group.links.slice(0, LINKS_SHOWN_AT_FIRST)

  return (
    <article className="group-card">
      <header className="group-card__header">
        <h3>{group.problem}</h3>
        <span className="group-card__meta">
          {group.isSameSite ? 'this site' : 'other sites'} · {plural(group.links.length, 'link')}
        </span>
        {group.advice && (
          <span className={`severity severity--${group.advice.severity}`}>{group.advice.severity}</span>
        )}
      </header>

      {group.advice && (
        <dl className="advice">
          <dt>Likely cause</dt>
          <dd>{group.advice.cause}</dd>
          <dt>How to fix it</dt>
          <dd>{group.advice.fix}</dd>
        </dl>
      )}

      <ul className="link-list">
        {links.map((link) => (
          <li key={link.url}>
            {/* noopener noreferrer: the scanned site can't control or see this tab */}
            <a href={link.url} target="_blank" rel="noopener noreferrer">
              {link.url}
            </a>
            <span className="link-list__detail">{describe(link)}</span>
          </li>
        ))}
      </ul>

      {group.links.length > LINKS_SHOWN_AT_FIRST && (
        <button type="button" className="link-button" onClick={() => setShowAll(!showAll)}>
          {showAll ? 'Show fewer' : `Show all ${group.links.length} links`}
        </button>
      )}
    </article>
  )
}

function describe(link: BrokenLink) {
  if (link.statusCode !== null) return `HTTP ${link.statusCode}`
  return link.error ?? 'No response'
}
