import { TicketStatusBadge } from './TicketStatusBadge'
import { Link } from 'react-router'
import { priorityLabels } from '../api/tickets'
import type { Ticket } from '../api/tickets'
import { formatUtcDate, utcTimestamp } from '../utils/dates'
export function TicketCards({ tickets }: { tickets: Ticket[] }) {
  return (
    <ul className="mobile-ticket-list">
      {tickets.map((ticket) => (
        <li key={ticket.id}>
          <Link
            to={`/tickets/${ticket.id}`}
            aria-label={`View ticket #${ticket.id}: ${ticket.title}`}
          >
            <span className="ticket-id">
              #{ticket.id} · {ticket.storeName || `Store #${ticket.storeId}`}
            </span>
            <span className="ticket-title">{ticket.title}</span>
            <div className="mobile-ticket-meta">
              <TicketStatusBadge ticket={ticket} />
              <span className={`priority-tag priority-${ticket.priority}`}>
                {priorityLabels[ticket.priority]}
              </span>
              <time dateTime={utcTimestamp(ticket.createdAt)}>
                {formatUtcDate(ticket.createdAt)}
              </time>
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}
