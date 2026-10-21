export type TicketStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7
export type TicketPriority = 1 | 2 | 3 | 4

export type Ticket = {
  id: number
  title: string
  description: string
  status: TicketStatus
  priority: TicketPriority
  storeId: number
  createdAt: string
}

export const statusLabels: Record<TicketStatus, string> = {
  1: 'Awaiting approval',
  2: 'Approved',
  3: 'Assigned',
  4: 'In progress',
  5: 'Resolved',
  6: 'Closed',
  7: 'Rejected',
}

export const priorityLabels: Record<TicketPriority, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

function isTicket(value: unknown): value is Ticket {
  if (typeof value !== 'object' || value === null) return false
  const ticket = value as Record<string, unknown>
  return typeof ticket.id === 'number' && Number.isInteger(ticket.id) && ticket.id > 0
    && typeof ticket.title === 'string'
    && typeof ticket.description === 'string'
    && typeof ticket.status === 'number' && Object.hasOwn(statusLabels, ticket.status)
    && typeof ticket.priority === 'number' && Object.hasOwn(priorityLabels, ticket.priority)
    && typeof ticket.storeId === 'number' && Number.isInteger(ticket.storeId) && ticket.storeId > 0
    && typeof ticket.createdAt === 'string' && !Number.isNaN(Date.parse(ticket.createdAt))
}

export async function getTickets(signal: AbortSignal): Promise<Ticket[]> {
  const response = await fetch('/api/tickets', {
    headers: { Accept: 'application/json' },
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })

  if (!response.ok) {
    throw new Error(`Tickets could not be loaded (HTTP ${response.status}). Please try again.`)
  }

  const data: unknown = await response.json()
  if (!Array.isArray(data) || !data.every(isTicket)) {
    throw new Error('The server returned unexpected ticket data. Please try again.')
  }

  return data
}
