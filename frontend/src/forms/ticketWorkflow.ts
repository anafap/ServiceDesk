import type { Ticket } from '../api/tickets'
import type { AuthUser } from '../auth/session'
import type { Technician, WorkflowAction } from '../api/ticketWorkflow'

export const actionLabels: Record<WorkflowAction, string> = {
  approve: 'Approve ticket', reject: 'Reject ticket', assign: 'Assign technician',
  progress: 'Start progress', resolve: 'Resolve ticket', close: 'Close ticket',
}

export function availableActions(ticket: Pick<Ticket, 'storeId' | 'status'>, user: AuthUser): WorkflowAction[] {
  if (user.role === 'StoreManager') {
    if (user.storeId !== ticket.storeId) return []
    return ticket.status === 3 ? ['progress'] : ticket.status === 4 ? ['resolve'] : ticket.status === 5 ? ['close'] : []
  }
  switch (ticket.status) {
    case 1: return ['approve', 'reject']
    case 2: return ['assign']
    case 3: return ['progress']
    case 4: return ['resolve']
    case 5: return ['close']
    default: return []
  }
}

export function validateAssignment(technicianId: string, scheduledAt: string, technicians: Technician[], now = Date.now()) {
  const errors: { technicianId?: string; scheduledAt?: string } = {}
  if (!technicians.some(technician => String(technician.id) === technicianId)) errors.technicianId = 'Select an available technician.'
  if (!scheduledAt || Number.isNaN(Date.parse(scheduledAt)) || Date.parse(scheduledAt) <= now) errors.scheduledAt = 'Choose a future visit time.'
  return errors
}
