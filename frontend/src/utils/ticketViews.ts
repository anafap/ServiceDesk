import type { Ticket, TicketPriority, TicketStatus } from '../api/tickets.ts'

export type TicketFilters = {
  title: string
  status: TicketStatus | 'all'
  priority: TicketPriority | 'all'
}

export function filterTickets(tickets: readonly Ticket[], filters: TicketFilters): Ticket[] {
  const title = filters.title.trim().toLowerCase()
  return tickets.filter((ticket) => ticket.title.toLowerCase().includes(title)
    && (filters.status === 'all' || ticket.status === filters.status)
    && (filters.priority === 'all' || ticket.priority === filters.priority))
}
