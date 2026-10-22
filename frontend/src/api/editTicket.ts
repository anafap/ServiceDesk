import { problemMessage } from './problems.ts'
import { apiFetch } from './http.ts'
import type { TicketPriority } from './tickets'

export type EditTicketRequest = { title: string; description: string; priority: TicketPriority; expectedVersion: string }

export async function editTicket(ticketId: number, request: EditTicketRequest, signal: AbortSignal): Promise<void> {
  let response: Response
  try {
    response = await apiFetch(`/api/tickets/${ticketId}`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch {
    throw new Error('We could not confirm the save. Reload the ticket before trying again.')
  }
  if (response.ok) return
  if (response.status === 409) throw new Error(await problemMessage(response, 'This ticket changed. Refresh before trying again.'))
  if (response.status === 404) throw new Error('This ticket is no longer available or you do not have access.')
  if (response.status >= 500) throw new Error('We could not confirm the save. Reload the ticket before trying again.')
  throw new Error(`Changes were not accepted (HTTP ${response.status}). Check your entries and try again.`)
}
