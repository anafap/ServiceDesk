import { displayName } from '../utils/displayNames'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { getTicketDetails, getTicketHistory, parseTicketId, TicketNotFoundError } from '../api/ticketDetails'
import type { TicketDetails, TicketHistoryEntry } from '../api/ticketDetails'
import { priorityLabels, statusLabels } from '../api/tickets'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { EditTicketForm } from '../components/EditTicketForm'
import { TicketWorkflow } from '../components/TicketWorkflow'
import { TicketCorrection } from '../components/TicketCorrection'
import { TicketAssignment } from '../components/TicketAssignment'
import { formatUtcDateTime, utcTimestamp } from '../utils/dates'

type DetailsState =
  | { status: 'loading' }
  | { status: 'success'; ticket: TicketDetails }
  | { status: 'not-found' }
  | { status: 'error'; message: string }

type HistoryState =
  | { status: 'loading' }
  | { status: 'success'; entries: TicketHistoryEntry[] }
  | { status: 'error'; message: string }

function errorMessage(error: unknown) {
  if (error instanceof Error && error.name === 'TimeoutError') return 'The request took too long. Please try again.'
  return error instanceof Error && error.name !== 'TypeError' && error.name !== 'SyntaxError'
    ? error.message : 'Unable to load this information. Check your connection and try again.'
}

function MissingTicket() {
  return <section className="panel"><EmptyState icon="tickets" title="Ticket not found" description="This ticket does not exist or you do not have access to it.">
    <Link className="button button-primary" to="/tickets">Back to tickets</Link>
  </EmptyState></section>
}

function TicketHistory({ ticketId }: { ticketId: number }) {
  const [state, setState] = useState<HistoryState>({ status: 'loading' })
  const [requestVersion, setRequestVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getTicketHistory(ticketId, controller.signal)
      .then((entries) => { if (!controller.signal.aborted) setState({ status: 'success', entries }) })
      .catch((error: unknown) => { if (!controller.signal.aborted) setState({ status: 'error', message: errorMessage(error) }) })
    return () => controller.abort()
  }, [ticketId, requestVersion])

  function reload() {
    setState({ status: 'loading' })
    setRequestVersion((version) => version + 1)
  }

  return (
    <section className="panel" aria-labelledby="history-title" aria-busy={state.status === 'loading'}>
      <div className="panel-heading"><h2 id="history-title">Status history</h2>
        <button className="button button-secondary" type="button" disabled={state.status === 'loading'} onClick={reload}>Refresh history</button></div>
      {state.status === 'loading' && <p className="loading-state" role="status">Loading history…</p>}
      {state.status === 'error' && <div role="alert"><EmptyState icon="tickets" title="History could not be loaded" description={state.message}>
        <button className="button button-primary" type="button" onClick={reload}>Try again</button>
      </EmptyState></div>}
      {state.status === 'success' && (state.entries.length === 0
        ? <EmptyState icon="tickets" title="No status changes yet" description="No status changes have been recorded for this ticket." />
        : <ol className="history-list">{state.entries.map((entry) => <li key={entry.id}>
            {entry.reversesHistoryId && <p className="workflow-note">Status correction · reverses event #{entry.reversesHistoryId}{entry.previousStatus === 3 && entry.newStatus === 2 ? ' · Assignment withdrawn; original record retained.' : entry.previousStatus === 2 && entry.newStatus === 3 ? ' · Recorded assignment restored.' : ''}</p>}
            <p className="history-transition">{entry.previousStatus === null ? 'Initial status' : statusLabels[entry.previousStatus]} → <strong>{statusLabels[entry.newStatus]}</strong></p>
            <p className="history-meta"><time dateTime={utcTimestamp(entry.changedAt)}>{formatUtcDateTime(entry.changedAt)} UTC</time><span>Recorded by {displayName(entry.changedByName, `User #${entry.changedByUserId}`)}</span></p>
            <p className="history-reason">{entry.reason.trim() ? entry.reason : 'No reason recorded.'}</p>
          </li>)}</ol>)}
    </section>
  )
}

function TicketDetailsContent({ ticketId }: { ticketId: number }) {
  const [state, setState] = useState<DetailsState>({ status: 'loading' })
  const [requestVersion, setRequestVersion] = useState(0)
  const [editing, setEditing] = useState(false)
  const [notice, setNotice] = useState('')
  const [workflowBusy, setWorkflowBusy] = useState(false)

  function refreshDetails(message = '') {
    setEditing(false)
    setNotice(message)
    setState({ status: 'loading' })
    setRequestVersion((version) => version + 1)
  }

  useEffect(() => {
    const controller = new AbortController()
    getTicketDetails(ticketId, controller.signal)
      .then((ticket) => { if (!controller.signal.aborted) setState({ status: 'success', ticket }) })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setState(error instanceof TicketNotFoundError
          ? { status: 'not-found' } : { status: 'error', message: errorMessage(error) })
      })
    return () => controller.abort()
  }, [ticketId, requestVersion])

  if (state.status === 'loading') return <p className="loading-state" role="status">Loading ticket #{ticketId}…</p>
  if (state.status === 'not-found') return <MissingTicket />
  if (state.status === 'error') return <section className="panel" role="alert"><EmptyState icon="tickets" title="Ticket could not be loaded" description={state.message}>
    <button className="button button-primary" type="button" onClick={() => { setState({ status: 'loading' }); setRequestVersion((version) => version + 1) }}>Try again</button>
  </EmptyState></section>

  const { ticket } = state
  return (
    <>
      <PageHeader eyebrow={`TICKET #${ticket.id}`} title={ticket.title} description="Maintenance request details and status history." />
      {notice && <p className="form-message success-message" role="status">{notice}</p>}
      {editing ? <EditTicketForm ticket={ticket} onCancel={() => setEditing(false)} onSaved={() => {
        refreshDetails('Ticket changes saved.')
      }} /> : <div className="page-actions workflow-buttons"><button className="button button-secondary" type="button" disabled={workflowBusy} onClick={() => refreshDetails()}>Refresh details</button><button className="button button-secondary" type="button" disabled={workflowBusy || ticket.status === 6 || ticket.status === 7} onClick={() => { setNotice(''); setEditing(true) }}>Edit ticket</button></div>}
      <section className="panel ticket-details" aria-labelledby="details-title">
        <div className="panel-heading"><h2 id="details-title">Ticket details</h2></div>
        <div className="detail-body">
          <dl className="ticket-metadata">
            <div><dt>Status</dt><dd><span className={`status-tag status-${ticket.status}`}>{statusLabels[ticket.status]}</span></dd></div>
            <div><dt>Priority</dt><dd><span className={`priority-tag priority-${ticket.priority}`}>{priorityLabels[ticket.priority]}</span></dd></div>
            <div><dt>Store</dt><dd>{displayName(ticket.storeName, `Store #${ticket.storeId}`)}</dd></div>
            <div><dt>Category</dt><dd>{displayName(ticket.categoryName, `Category #${ticket.categoryId}`)}</dd></div>
            <div><dt>Created by</dt><dd>{displayName(ticket.createdByName, `User #${ticket.createdByUserId}`)}</dd></div>
            <div><dt>Created (UTC)</dt><dd><time dateTime={utcTimestamp(ticket.createdAt)}>{formatUtcDateTime(ticket.createdAt)}</time></dd></div>
            {ticket.assetId !== null && <div><dt>Asset</dt><dd>Asset #{ticket.assetId}</dd></div>}
          </dl>
          <h3 className="description-title">Description</h3>
          <p className="full-description">{ticket.description}</p>
        </div>
      </section>
      {!editing && <TicketWorkflow key={`workflow-${requestVersion}`} ticket={ticket} onUpdated={refreshDetails} onBusyChange={setWorkflowBusy} />}
      {ticket.status >= 3 && ticket.status <= 6 && <TicketAssignment key={`assignment-${requestVersion}`} ticketId={ticket.id} />}
      {!editing && <TicketCorrection key={`correction-${requestVersion}`} ticket={ticket} onUpdated={refreshDetails} busy={workflowBusy} onBusyChange={setWorkflowBusy} />}
      <TicketHistory key={`history-${requestVersion}`} ticketId={ticket.id} />
    </>
  )
}

export function TicketDetailsPage() {
  const { ticketId } = useParams()
  const id = parseTicketId(ticketId)
  return (
    <>
      <Link className="back-link" to="/tickets">← Back to tickets</Link>
      {id === null ? <MissingTicket /> : <TicketDetailsContent key={id} ticketId={id} />}
    </>
  )
}
