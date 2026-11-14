import { validVersion } from './problems.ts'
import { validDisplayNames } from '../utils/displayNames.ts'
import { apiFetch } from './http.ts'

export type TicketStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7
export type TicketPriority = 1 | 2 | 3 | 4

export type Ticket = {
  id: number
  title: string
  description: string
  status: TicketStatus
  visitState?: 'AwaitingConfirmation' | 'Confirmed' | 'Declined' | null
  priority: TicketPriority
  storeId: number
  storeName?: string | null
  categoryName?: string | null
  createdByName?: string | null
  version: string
  createdAt: string
}

export type CreateTicketRequest = {
  title: string
  description: string
  categoryId: number
  storeId: number
  priority: TicketPriority
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

export function ticketStatusLabel(ticket: Pick<Ticket, 'status' | 'visitState'>): string {
  if (ticket.status === 3) {
    if (ticket.visitState === 'Declined') return 'Visit time declined'
    if (ticket.visitState === 'Confirmed') return 'Visit confirmed'
    if (ticket.visitState === 'AwaitingConfirmation') return 'Awaiting visit confirmation'
  }
  return statusLabels[ticket.status]
}

export const priorityLabels: Record<TicketPriority, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

export function isTicket(value: unknown): value is Ticket {
  if (typeof value !== 'object' || value === null) return false
  const ticket = value as Record<string, unknown>
  return (
    (ticket.visitState === undefined || ticket.visitState === null || ['AwaitingConfirmation', 'Confirmed', 'Declined'].includes(String(ticket.visitState))) &&
    validVersion(ticket.version) &&
    validDisplayNames(ticket, ['storeName', 'categoryName', 'createdByName']) &&
    typeof ticket.id === 'number' &&
    Number.isInteger(ticket.id) &&
    ticket.id > 0 &&
    typeof ticket.title === 'string' &&
    typeof ticket.description === 'string' &&
    typeof ticket.status === 'number' &&
    Object.hasOwn(statusLabels, ticket.status) &&
    typeof ticket.priority === 'number' &&
    Object.hasOwn(priorityLabels, ticket.priority) &&
    typeof ticket.storeId === 'number' &&
    Number.isInteger(ticket.storeId) &&
    ticket.storeId > 0 &&
    typeof ticket.createdAt === 'string' &&
    !Number.isNaN(Date.parse(ticket.createdAt))
  )
}

export type TicketPage = {
  items: Ticket[]
  totalCount: number
  page: number
  pageSize: number
}
export type TicketQuery = {
  page?: number
  pageSize?: number
  title?: string
  status?: TicketStatus | 'all'
  priority?: TicketPriority | 'all'
  openOnly?: boolean
}
export async function getTickets(
  signal: AbortSignal,
  query: TicketQuery = {},
): Promise<TicketPage> {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query))
    if (value !== undefined && value !== 'all' && value !== '')
      params.set(key, String(value))
  const response = await apiFetch(
    `/api/tickets${params.size ? `?${params}` : ''}`,
    {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    },
  )
  if (!response.ok)
    throw new Error(
      `Tickets could not be loaded (HTTP ${response.status}). Please try again.`,
    )
  const data: unknown = await response.json()
  if (!data || typeof data !== 'object')
    throw new Error('The server returned unexpected ticket data.')
  const page = data as Record<string, unknown>
  if (
    !Array.isArray(page.items) ||
    !page.items.every(isTicket) ||
    !Number.isInteger(page.totalCount) ||
    Number(page.totalCount) < 0 ||
    !Number.isInteger(page.page) ||
    Number(page.page) < 1 ||
    !Number.isInteger(page.pageSize) ||
    Number(page.pageSize) < 1 ||
    Number(page.pageSize) > 100 ||
    page.items.length > Number(page.pageSize)
  )
    throw new Error('The server returned unexpected ticket data.')
  return data as TicketPage
}
export type Dashboard = {
  summary: { total: number; open: number; inProgress: number; resolved: number }
  recent: Ticket[]
  attention: { ticket: Ticket; action: string }[]
}
export async function getDashboard(signal: AbortSignal): Promise<Dashboard> {
  const response = await apiFetch('/api/dashboard', {
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })
  if (!response.ok)
    throw new Error(`Dashboard could not be loaded (HTTP ${response.status}).`)
  const data: unknown = await response.json()
  if (!data || typeof data !== 'object')
    throw new Error('Unexpected dashboard response.')
  const value = data as Record<string, unknown>
  if (
    !value.summary ||
    typeof value.summary !== 'object' ||
    !['total', 'open', 'inProgress', 'resolved'].every((key) => {
      const n = (value.summary as Record<string, unknown>)[key]
      return Number.isInteger(n) && Number(n) >= 0
    }) ||
    !Array.isArray(value.recent) ||
    value.recent.length > 5 ||
    !value.recent.every(isTicket)
  )
    throw new Error('Unexpected dashboard response.')
  if (
    !Array.isArray(value.attention) ||
    value.attention.length > 8 ||
    !value.attention.every(
      (item) =>
        item &&
        typeof item === 'object' &&
        isTicket(item.ticket) &&
        typeof item.action === 'string',
    )
  )
    throw new Error('Unexpected dashboard attention response.')
  return data as Dashboard
}

export async function createTicket(
  request: CreateTicketRequest,
  signal: AbortSignal,
): Promise<Ticket> {
  let response: Response
  const uncertainMessage =
    'We could not confirm whether the ticket was saved. Refresh the ticket list before submitting again.'

  try {
    response = await apiFetch('/api/tickets', {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch {
    throw new Error(uncertainMessage)
  }

  if (!response.ok) {
    if (response.status >= 500) throw new Error(uncertainMessage)
    throw new Error(
      `The ticket was not accepted (HTTP ${response.status}). Check your entries and try again.`,
    )
  }

  try {
    const data: unknown = await response.json()
    if (!isTicket(data)) throw new Error('Unexpected ticket response')
    return data
  } catch {
    throw new Error(uncertainMessage)
  }
}
