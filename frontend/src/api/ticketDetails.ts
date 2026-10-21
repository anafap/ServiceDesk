import { apiFetch } from './http.ts'
import { isTicket, statusLabels } from './tickets.ts'
import type { Ticket, TicketStatus } from './tickets.ts'

export type TicketDetails = Ticket & { categoryId: number; createdByUserId: number; assetId: number | null }
export type TicketHistoryEntry = {
  id: number
  supportTicketId: number
  previousStatus: TicketStatus | null
  newStatus: TicketStatus
  changedByUserId: number
  changedAt: string
  reason: string
}

export class TicketNotFoundError extends Error {
  constructor() {
    super('This ticket does not exist or you do not have access to it.')
    this.name = 'TicketNotFoundError'
  }
}

export function parseTicketId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const id = Number(value)
  return Number.isInteger(id) && id <= 2_147_483_647 ? id : null
}

function positiveId(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0
}

async function load(path: string, signal: AbortSignal): Promise<unknown> {
  const response = await apiFetch(path, {
    headers: { Accept: 'application/json' },
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })
  if (response.status === 404) throw new TicketNotFoundError()
  if (!response.ok) throw new Error(`The request failed (HTTP ${response.status}). Please try again.`)
  return response.json()
}

export async function getTicketDetails(ticketId: number, signal: AbortSignal): Promise<TicketDetails> {
  const data = await load(`/api/tickets/${ticketId}`, signal)
  if (!isTicket(data)) throw new Error('The server returned unexpected ticket details.')
  const details = data as Ticket & Record<string, unknown>
  if (details.id !== ticketId || !positiveId(details.categoryId) || !positiveId(details.createdByUserId)
    || (details.assetId !== null && !positiveId(details.assetId))) throw new Error('The server returned unexpected ticket details.')
  return details as TicketDetails
}

export async function getTicketHistory(ticketId: number, signal: AbortSignal): Promise<TicketHistoryEntry[]> {
  const data = await load(`/api/tickets/${ticketId}/history`, signal)
  if (!Array.isArray(data) || !data.every((entry: unknown) => {
    if (typeof entry !== 'object' || entry === null) return false
    const item = entry as Record<string, unknown>
    return positiveId(item.id) && item.supportTicketId === ticketId
      && (item.previousStatus === null || typeof item.previousStatus === 'number' && Object.hasOwn(statusLabels, item.previousStatus))
      && typeof item.newStatus === 'number' && Object.hasOwn(statusLabels, item.newStatus)
      && positiveId(item.changedByUserId) && typeof item.reason === 'string'
      && typeof item.changedAt === 'string' && !Number.isNaN(Date.parse(item.changedAt))
  })) throw new Error('The server returned unexpected ticket history.')
  return data as TicketHistoryEntry[]
}
