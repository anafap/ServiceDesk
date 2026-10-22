import { problemMessage } from './problems.ts'
import { validDisplayNames } from '../utils/displayNames.ts'
import { apiFetch } from './http.ts'

export type WorkflowAction = 'approve' | 'reject' | 'assign' | 'progress' | 'resolve' | 'close'
export type Technician = { id: number; fullName: string; companyName: string }
export type Assignment = {
  id: number; supportTicketId: number; externalTechnicianId: number; assignedByUserId: number
  technicianName?: string | null; assignedByName?: string | null
  assignedAt: string; scheduledAt: string | null; notes: string
}
export type AssignmentRequest = { externalTechnicianId: number; scheduledAt: string; notes: string; reason: string | null; expectedVersion: string }

export async function getTechnicians(signal: AbortSignal): Promise<Technician[]> {
  const response = await apiFetch('/api/external-technicians', { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error('Technicians could not be loaded. Please try again.')
  const data: unknown = await response.json()
  if (!Array.isArray(data) || !data.every((item: unknown) => {
    if (typeof item !== 'object' || item === null) return false
    const entry = item as Record<string, unknown>
    return typeof entry.id === 'number' && Number.isInteger(entry.id) && entry.id > 0
      && typeof entry.fullName === 'string' && entry.fullName.trim().length > 0 && typeof entry.companyName === 'string'
  })) throw new Error('The server returned unexpected technician data.')
  return data as Technician[]
}

export async function getAssignment(ticketId: number, signal: AbortSignal): Promise<Assignment | null> {
  const response = await apiFetch(`/api/tickets/${ticketId}/assignment`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (response.status === 204) return null
  if (!response.ok) throw new Error('The saved assignment could not be loaded.')
  const data: unknown = await response.json()
  if (typeof data !== 'object' || data === null) throw new Error('Unexpected assignment data.')
  const assignment = data as Record<string, unknown>
  if (!validDisplayNames(assignment, ['technicianName', 'assignedByName']) || assignment.supportTicketId !== ticketId
    || ![assignment.id, assignment.externalTechnicianId, assignment.assignedByUserId].every(value => typeof value === 'number' && Number.isInteger(value) && value > 0)
    || typeof assignment.assignedAt !== 'string' || Number.isNaN(Date.parse(assignment.assignedAt))
    || (assignment.scheduledAt !== null && (typeof assignment.scheduledAt !== 'string' || Number.isNaN(Date.parse(assignment.scheduledAt))))
    || typeof assignment.notes !== 'string') throw new Error('Unexpected assignment data.')
  return assignment as Assignment
}

export async function performTicketAction(ticketId: number, action: WorkflowAction, request: { reason: string | null; expectedVersion: string } | AssignmentRequest, signal: AbortSignal): Promise<void> {
  let response: Response
  const uncertain = 'We could not confirm the update. Refresh ticket details before trying again.'
  try {
    response = await apiFetch(`/api/tickets/${ticketId}/${action}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch { throw new Error(uncertain) }
  if (response.ok) return
  if (response.status === 403) throw new Error('You do not have permission to perform this action.')
  if (response.status === 404) throw new Error('This ticket is no longer available or you do not have access.')
  if (response.status === 409) throw new Error(await problemMessage(response, 'This ticket changed. Refresh ticket details and try again.'))
  if (response.status >= 500) throw new Error(uncertain)
  throw new Error('The update was not accepted. Check the technician, visit time, and other entries before trying again.')
}
