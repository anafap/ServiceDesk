import { ticketStatusLabel } from '../api/tickets'
import type { Ticket } from '../api/tickets'

export function TicketStatusBadge({ ticket }: { ticket: Pick<Ticket, 'status' | 'visitState'> }) {
  const stateClass = ticket.status === 3 && ticket.visitState === 'Declined' ? 'status-visit-declined'
    : ticket.status === 3 && ticket.visitState === 'Confirmed' ? 'status-visit-confirmed' : `status-${ticket.status}`
  return <span className={`status-tag ${stateClass}`}>{ticketStatusLabel(ticket)}</span>
}
