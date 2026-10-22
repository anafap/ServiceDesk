import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import type { TicketDetails } from '../api/ticketDetails'
import { getTechnicians, performTicketAction } from '../api/ticketWorkflow'
import type { Technician, WorkflowAction } from '../api/ticketWorkflow'
import { useSession } from '../auth/useSession'
import { actionLabels, availableActions, validateAssignment } from '../forms/ticketWorkflow'

type TechnicianState = { status: 'loading' } | { status: 'error' } | { status: 'success'; technicians: Technician[] }
const successMessages: Record<WorkflowAction, string> = {
  approve: 'Ticket approved.', reject: 'Ticket rejected.', assign: 'Technician assigned and visit scheduled.',
  progress: 'Ticket is now in progress.', resolve: 'Ticket marked as resolved.', close: 'Ticket closed.',
}

export function TicketWorkflow({ ticket, onUpdated, onBusyChange }: {
  ticket: TicketDetails; onUpdated: (message: string) => void; onBusyChange: (busy: boolean) => void
}) {
  const session = useSession()
  const [action, setAction] = useState<WorkflowAction | null>(null)
  const [reason, setReason] = useState('')
  const [technicianId, setTechnicianId] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [notes, setNotes] = useState('')
  const [technicians, setTechnicians] = useState<TechnicianState>({ status: 'loading' })
  const [lookupVersion, setLookupVersion] = useState(0)
  const [errors, setErrors] = useState<{ technicianId?: string; scheduledAt?: string }>({})
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef<AbortController | null>(null)
  const actionHeading = useRef<HTMLHeadingElement>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => { if (action) actionHeading.current?.focus() }, [action])
  useEffect(() => {
    if (action !== 'assign') return
    const controller = new AbortController()
    getTechnicians(controller.signal)
      .then((technicians) => { if (!controller.signal.aborted) setTechnicians({ status: 'success', technicians }) })
      .catch(() => { if (!controller.signal.aborted) setTechnicians({ status: 'error' }) })
    return () => controller.abort()
  }, [action, lookupVersion])

  if (!session) return null
  const actions = availableActions(ticket, session.user)

  function chooseAction(action: WorkflowAction) {
    setAction(action); setMessage(''); setErrors({}); setReason('')
    if (action === 'assign') setTechnicians({ status: 'loading' })
  }

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!action || pending.current || !actions.includes(action)) return
    const isAssignment = action === 'assign'
    if (isAssignment && technicians.status !== 'success') return
    const validation = isAssignment && technicians.status === 'success'
      ? validateAssignment(technicianId, scheduledAt, technicians.technicians) : {}
    setErrors(validation); setMessage('')
    if (Object.keys(validation).length) {
      const field = event.currentTarget.elements.namedItem(Object.keys(validation)[0])
      if (field instanceof HTMLElement) field.focus()
      return
    }
    const controller = new AbortController()
    pending.current = controller; setSaving(true); onBusyChange(true)
    try {
      const request = isAssignment
        ? { reason: reason.trim() || null, externalTechnicianId: Number(technicianId), scheduledAt: new Date(scheduledAt).toISOString(), notes: notes.trim(), expectedVersion: ticket.version }
        : { reason: reason.trim() || null, expectedVersion: ticket.version }
      await performTicketAction(ticket.id, action, request, controller.signal)
      if (!controller.signal.aborted) onUpdated(successMessages[action])
    } catch (error: unknown) {
      if (!controller.signal.aborted) setMessage(error instanceof Error ? error.message : 'The update could not be saved.')
    } finally {
      pending.current = null
      onBusyChange(false)
      if (!controller.signal.aborted) setSaving(false)
    }
  }

  const assignmentUnavailable = action === 'assign' && (technicians.status !== 'success' || technicians.technicians.length === 0)

  return <section className="panel ticket-details" aria-labelledby="workflow-title">
    <div className="panel-heading"><h2 id="workflow-title">Workflow</h2></div>
    <div className="ticket-form-content">
      {actions.length === 0 && <p className="workflow-note">{ticket.status === 6 ? 'This ticket is closed.' : ticket.status === 7 ? 'This ticket was rejected.' : 'No workflow actions are available for your role at this stage.'}</p>}
      {!action && <div className="workflow-buttons">{actions.map((action) => <button key={action} className={`button ${action === 'reject' ? 'button-danger' : 'button-primary'}`} type="button" onClick={() => chooseAction(action)}>{actionLabels[action]}</button>)}</div>}
      {action && <form noValidate onSubmit={submit} aria-busy={saving}>
        <h3 className="action-title" ref={actionHeading} tabIndex={-1}>{actionLabels[action]}</h3>
        {action === 'reject' && <p className="workflow-note">Rejecting ends this request. A rejected ticket cannot continue through the workflow.</p>}
        {action === 'close' && <p className="workflow-note">Confirm the issue has been resolved before closing this ticket.</p>}
        {action === 'assign' && technicians.status === 'loading' && <p role="status" className="lookup-message">Loading active technicians…</p>}
        {action === 'assign' && technicians.status === 'error' && <div className="form-message error-message" role="alert"><p>Technicians could not be loaded.</p><button className="button button-secondary" type="button" onClick={() => { setTechnicians({ status: 'loading' }); setLookupVersion((version) => version + 1) }}>Try again</button></div>}
        {action === 'assign' && technicians.status === 'success' && technicians.technicians.length === 0 && <p className="form-message" role="status">No active technicians are available. An administrator must add a technician before assignment.</p>}
        <fieldset className="ticket-fields" disabled={saving}>
          <legend className="visually-hidden">Confirm workflow action</legend>
          {action === 'assign' && <>
            <div className="form-field"><label htmlFor="assign-technician">External technician</label>
              <select id="assign-technician" name="technicianId" value={technicianId} required disabled={assignmentUnavailable} onChange={(event) => { setTechnicianId(event.target.value); setErrors((current) => ({ ...current, technicianId: undefined })) }} aria-invalid={Boolean(errors.technicianId)} aria-describedby={errors.technicianId ? 'technician-error' : undefined}>
                <option value="">Select a technician</option>{technicians.status === 'success' && technicians.technicians.map((technician) => <option key={technician.id} value={technician.id}>{technician.fullName} · {technician.companyName}</option>)}
              </select>{errors.technicianId && <p id="technician-error" className="field-error">{errors.technicianId}</p>}</div>
            <div className="form-field"><label htmlFor="assign-visit">Visit time (your local time)</label>
              <input id="assign-visit" name="scheduledAt" type="datetime-local" value={scheduledAt} required onChange={(event) => { setScheduledAt(event.target.value); setErrors((current) => ({ ...current, scheduledAt: undefined })) }} aria-invalid={Boolean(errors.scheduledAt)} aria-describedby={errors.scheduledAt ? 'visit-error visit-hint' : 'visit-hint'} />
              <p id="visit-hint" className="field-hint">The saved visit time is displayed in UTC.</p>{errors.scheduledAt && <p id="visit-error" className="field-error">{errors.scheduledAt}</p>}</div>
            <div className="form-field field-full"><label htmlFor="assign-notes">Visit notes (optional)</label><textarea id="assign-notes" value={notes} rows={3} maxLength={4000} onChange={(event) => setNotes(event.target.value)} /></div>
          </>}
          <div className="form-field field-full"><label htmlFor="workflow-reason">Reason (optional)</label><textarea id="workflow-reason" value={reason} rows={2} maxLength={4000} onChange={(event) => setReason(event.target.value)} /></div>
        </fieldset>
        {message && <p className="form-message error-message" role="alert">{message}</p>}
        <div className="form-actions"><button className="button button-secondary" type="button" disabled={saving} onClick={() => setAction(null)}>Cancel</button>
          <button className={`button ${action === 'reject' ? 'button-danger' : 'button-primary'}`} type="submit" disabled={saving || assignmentUnavailable}>{saving ? 'Saving…' : `Confirm: ${actionLabels[action].toLowerCase()}`}</button></div>
      </form>}
    </div>
  </section>
}
