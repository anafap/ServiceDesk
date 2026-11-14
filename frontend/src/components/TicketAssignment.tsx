import { displayName } from '../utils/displayNames'
import { useEffect, useRef, useState } from 'react'
import { changeVisit, getAssignment } from '../api/ticketWorkflow'
import type { Assignment, VisitAction } from '../api/ticketWorkflow'
import { formatUtcDateTime, utcTimestamp } from '../utils/dates'

import type { TicketDetails } from '../api/ticketDetails'
import { useSession } from '../auth/useSession'
import type { SubmitEvent } from 'react'

type AssignmentState = { status: 'loading' } | { status: 'success'; assignment: Assignment | null } | { status: 'error' }

export function TicketAssignment({ ticket, busy, onUpdated, onBusyChange }: {
  ticket: TicketDetails; busy: boolean; onUpdated: (message: string) => void; onBusyChange: (busy: boolean) => void
}) {
  const ticketId = ticket.id
  const [state, setState] = useState<AssignmentState>({ status: 'loading' })
  const [version, setVersion] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    getAssignment(ticketId, controller.signal)
      .then((assignment) => { if (!controller.signal.aborted) setState({ status: 'success', assignment }) })
      .catch(() => { if (!controller.signal.aborted) setState({ status: 'error' }) })
    return () => controller.abort()
  }, [ticketId, version])
  return <section className="panel ticket-details" aria-labelledby="assignment-title" aria-busy={state.status === 'loading'}>
    <div className="panel-heading"><h2 id="assignment-title">Technician assignment</h2></div>
    <div className="detail-body">
      {state.status === 'loading' && <p className="workflow-note" role="status">Loading assignment…</p>}
      {state.status === 'error' && <div role="alert"><p className="workflow-note">The saved assignment could not be loaded.</p><button className="button button-secondary" type="button" onClick={() => { setState({ status: 'loading' }); setVersion((version) => version + 1) }}>Try again</button></div>}
      {state.status === 'success' && (state.assignment === null ? <p className="workflow-note">No technician assignment has been recorded.</p> : <>
        <dl className="ticket-metadata">
          <div><dt>Technician</dt><dd>{displayName(state.assignment.technicianName, `Technician #${state.assignment.externalTechnicianId}`)}</dd></div>
          <div><dt>Scheduled visit</dt><dd>{state.assignment.scheduledAt ? <time dateTime={utcTimestamp(state.assignment.scheduledAt)}>{formatUtcDateTime(state.assignment.scheduledAt)}</time> : 'Not scheduled'}</dd></div>
          <div><dt>Assigned by</dt><dd>{displayName(state.assignment.assignedByName, `User #${state.assignment.assignedByUserId}`)}</dd></div>

          <div><dt>Visit readiness</dt><dd>{ticket.status >= 4 && !state.assignment.confirmedAt ? 'Recorded before visit confirmation' : state.assignment.visitState === 'Confirmed' ? 'Visit confirmed' : state.assignment.visitState === 'Declined' ? 'Time declined' : 'Awaiting store confirmation'}</dd></div>
          {state.assignment.contactName && <div><dt>Store contact</dt><dd>{state.assignment.contactName} · {state.assignment.contactPhone}</dd></div>}
          {state.assignment.confirmedAt && <div><dt>Confirmed</dt><dd>{formatUtcDateTime(state.assignment.confirmedAt)} · {displayName(state.assignment.confirmedByName, `User #${state.assignment.confirmedByUserId}`)}</dd></div>}
          {state.assignment.repairStartedAt && <div><dt>Repair started</dt><dd>{formatUtcDateTime(state.assignment.repairStartedAt)}</dd></div>}
          {state.assignment.declineReason && <div><dt>Decline reason</dt><dd className="full-description">{state.assignment.declineReason}</dd></div>}
        </dl>
        {ticket.status === 3 && <VisitActions ticket={ticket} assignment={state.assignment} busy={busy} onUpdated={onUpdated} onBusyChange={onBusyChange} />}
        <h3 className="description-title">Visit notes</h3><p className="full-description">{state.assignment.notes.trim() ? state.assignment.notes : 'No visit notes recorded.'}</p>
      </>)}
    </div>
  </section>
}

function VisitActions({ ticket, assignment, busy, onUpdated, onBusyChange }: {
  ticket: TicketDetails; assignment: Assignment; busy: boolean
  onUpdated: (message: string) => void; onBusyChange: (busy: boolean) => void
}) {
  const session = useSession()
  const [action, setAction] = useState<VisitAction | null>(null)
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [reason, setReason] = useState('')
  const [time, setTime] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef<AbortController | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => { if (action) heading.current?.focus() }, [action])
  if (!session) return null
  const manager = session.user.role === 'StoreManager' && session.user.storeId === ticket.storeId
  const admin = session.user.role === 'Admin'
  const actions: VisitAction[] = [
    ...(manager && assignment.visitState === 'AwaitingConfirmation' ? ['confirm', 'decline'] as const : []),
    ...(admin ? ['reschedule'] as const : []),
    ...((manager || admin) && assignment.visitState === 'Confirmed' ? ['start'] as const : []),
  ]
  const labels: Record<VisitAction, string> = { confirm: 'Confirm visit', decline: 'Decline visit time', reschedule: 'Change visit time', start: 'Start repair' }
  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!action || pending.current || busy || !actions.includes(action)) return
    if (action === 'confirm' && (!name.trim() || name.trim().length > 100 || !phone.trim() || phone.trim().length > 50)) {
      setError('Enter a contact name (up to 100 characters) and phone (up to 50 characters).'); return
    }
    if (action === 'decline' && (!reason.trim() || reason.trim().length > 2000)) { setError('Enter a reason of up to 2,000 characters.'); return }
    if (action === 'reschedule' && (!time || Number.isNaN(Date.parse(time)) || Date.parse(time) <= Date.now())) { setError('Choose a future visit time.'); return }
    const controller = new AbortController()
    pending.current = controller; setSaving(true); onBusyChange(true); setError('')
    try {
      await changeVisit(ticket.id, action, {
        expectedVersion: ticket.version, expectedAssignmentVersion: assignment.version,
        ...(action === 'confirm' ? { contactName: name.trim(), contactPhone: phone.trim() } : {}),
        ...(action === 'decline' ? { reason: reason.trim() } : {}),
        ...(action === 'reschedule' ? { scheduledAt: new Date(time).toISOString() } : {}),
      }, controller.signal)
      if (!controller.signal.aborted) onUpdated(action === 'start' ? 'Repair started.' : action === 'confirm' ? 'Visit confirmed.' : action === 'decline' ? 'Visit time declined. Admin must propose another time.' : 'Visit time changed. Store confirmation is required again.')
    } catch (error: unknown) {
      if (!controller.signal.aborted) setError(error instanceof Error ? error.message : 'Visit could not be updated.')
    } finally {
      pending.current = null; onBusyChange(false)
      if (!controller.signal.aborted) setSaving(false)
    }
  }
  return <div className="visit-actions">
    {!action && <>
      <p className="workflow-note">{assignment.visitState === 'Confirmed' ? 'Record the technician’s arrival when repair work begins.' : assignment.visitState === 'Declined' ? 'Waiting for Admin to propose another visit time.' : 'The store manager must confirm the proposed time and provide a contact before repair can start.'}</p>
      <div className="workflow-buttons">{actions.map(value => <button key={value} type="button" className={`button ${value === 'decline' || value === 'reschedule' ? 'button-secondary' : 'button-primary'}`} disabled={busy} onClick={() => { setAction(value); setError('') }}>{labels[value]}</button>)}</div>
    </>}
    {action && <form onSubmit={submit} noValidate aria-busy={saving}>
      <h3 className="action-title" ref={heading} tabIndex={-1}>{labels[action]}</h3>
      {action === 'confirm' && <p className="workflow-note">Confirm the proposed visit time and name the person who will meet the technician.</p>}
      {action === 'reschedule' && <p className="workflow-note">The store manager must confirm the new time and contact details again.</p>}
      {action === 'start' && <p className="workflow-note">Confirm the technician has arrived and repair work is starting.</p>}
      <fieldset className="ticket-fields" disabled={busy || saving}>
        <legend className="visually-hidden">Visit details</legend>
        {action === 'confirm' && <>
          <div className="form-field field-full"><label htmlFor="visit-contact-name">Contact name</label><input id="visit-contact-name" value={name} maxLength={100} required onChange={event => setName(event.target.value)} /></div>
          <div className="form-field field-full"><label htmlFor="visit-contact-phone">Contact phone</label><input id="visit-contact-phone" type="tel" value={phone} maxLength={50} required onChange={event => setPhone(event.target.value)} /></div>
        </>}
        {action === 'decline' && <div className="form-field field-full"><label htmlFor="visit-decline-reason">Reason</label><textarea id="visit-decline-reason" value={reason} maxLength={2000} required rows={3} onChange={event => setReason(event.target.value)} /></div>}
        {action === 'reschedule' && <div className="form-field field-full"><label htmlFor="visit-new-time">Visit time (your local time)</label><input id="visit-new-time" type="datetime-local" value={time} required onChange={event => setTime(event.target.value)} /><p className="field-hint">Times are shown in your local timezone.</p></div>}
      </fieldset>
      {error && <p className="form-message error-message" role="alert">{error}</p>}
      <div className="form-actions"><button type="button" className="button button-secondary" disabled={saving} onClick={() => setAction(null)}>Cancel</button><button type="submit" className="button button-primary" disabled={busy || saving}>{saving ? 'Saving…' : 'Confirm'}</button></div>
    </form>}
  </div>
}
