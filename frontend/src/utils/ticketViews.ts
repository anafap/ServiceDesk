import type { Ticket, TicketPriority, TicketStatus } from '../api/tickets.ts'
import { utcTimestamp } from './dates.ts'

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

export function summarizeTickets(tickets: readonly Ticket[]) {
  return {
    total: tickets.length,
    open: tickets.filter((ticket) => ticket.status === 1 || ticket.status === 2 || ticket.status === 3).length,
    inProgress: tickets.filter((ticket) => ticket.status === 4).length,
    resolved: tickets.filter((ticket) => ticket.status === 5).length,
  }
}

export function recentTickets(tickets: readonly Ticket[], limit = 5): Ticket[] {
  return [...tickets].sort((left, right) =>
    Date.parse(utcTimestamp(right.createdAt)) - Date.parse(utcTimestamp(left.createdAt)) || right.id - left.id,
  ).slice(0, Math.max(0, limit))
}
