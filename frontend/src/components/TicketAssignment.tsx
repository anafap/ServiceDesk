import { useEffect, useState } from 'react'
import { getAssignment } from '../api/ticketWorkflow'
import type { Assignment } from '../api/ticketWorkflow'
import { formatUtcDateTime, utcTimestamp } from '../utils/dates'

type AssignmentState = { status: 'loading' } | { status: 'success'; assignment: Assignment | null } | { status: 'error' }

export function TicketAssignment({ ticketId }: { ticketId: number }) {
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
          <div><dt>Technician</dt><dd>Technician #{state.assignment.externalTechnicianId}</dd></div>
          <div><dt>Visit time (UTC)</dt><dd>{state.assignment.scheduledAt ? <time dateTime={utcTimestamp(state.assignment.scheduledAt)}>{formatUtcDateTime(state.assignment.scheduledAt)}</time> : 'Not scheduled'}</dd></div>
          <div><dt>Assigned by</dt><dd>User #{state.assignment.assignedByUserId}</dd></div>
        </dl>
        <h3 className="description-title">Visit notes</h3><p className="full-description">{state.assignment.notes.trim() ? state.assignment.notes : 'No visit notes recorded.'}</p>
      </>)}
    </div>
  </section>
}
