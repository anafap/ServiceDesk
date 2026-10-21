import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { getTickets, priorityLabels, statusLabels } from '../api/tickets'
import type { Ticket } from '../api/tickets'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { formatUtcDate, utcTimestamp } from '../utils/dates'
import { recentTickets, summarizeTickets } from '../utils/ticketViews'

type OverviewState =
  | { status: 'loading' }
  | { status: 'success'; tickets: Ticket[] }
  | { status: 'error'; message: string }

export function OverviewPage() {
  const [state, setState] = useState<OverviewState>({ status: 'loading' })
  const [requestVersion, setRequestVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getTickets(controller.signal)
      .then((tickets) => {
        if (!controller.signal.aborted) setState({ status: 'success', tickets })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        const message = error instanceof Error && error.name === 'TimeoutError'
          ? 'The request took too long. Please try again.'
          : error instanceof Error && error.name !== 'TypeError'
            ? error.message
            : 'Unable to reach the ticket service. Check your connection and try again.'
        setState({ status: 'error', message })
      })
    return () => controller.abort()
  }, [requestVersion])

  function refresh() {
    setState({ status: 'loading' })
    setRequestVersion((version) => version + 1)
  }

  const summary = state.status === 'success' ? summarizeTickets(state.tickets) : null
  const recent = state.status === 'success' ? recentTickets(state.tickets) : []

  return (
    <>
      <PageHeader eyebrow="YOUR WORKSPACE" title="Workspace overview" description="Monitor the maintenance tickets you have access to." />
      <div className="page-actions">
        <button className="button button-secondary" type="button" disabled={state.status === 'loading'} onClick={refresh}>Refresh dashboard</button>
        <Link className="button button-primary" to="/tickets">View all tickets</Link>
      </div>
      <div aria-busy={state.status === 'loading'}>
        {state.status === 'loading' && <p className="loading-state" role="status">Loading dashboard…</p>}
        {state.status === 'error' && <section className="panel" role="alert">
          <EmptyState icon="overview" title="We couldn't load your dashboard" description={state.message}>
            <button className="button button-primary" type="button" onClick={refresh}>Try again</button>
          </EmptyState>
        </section>}
        {summary && <>
          <section aria-label="Ticket summaries">
            <dl className="summary-grid">
              <div className="panel summary-card"><dt>Total tickets</dt><dd>{summary.total}<p>All accessible tickets</p></dd></div>
              <div className="panel summary-card"><dt>Open tickets</dt><dd>{summary.open}<p>Awaiting approval, approved, or assigned</p></dd></div>
              <div className="panel summary-card"><dt>In progress</dt><dd>{summary.inProgress}<p>Maintenance work underway</p></dd></div>
              <div className="panel summary-card"><dt>Resolved</dt><dd>{summary.resolved}<p>Awaiting closure confirmation</p></dd></div>
            </dl>
          </section>
          <section className="panel" aria-labelledby="recent-tickets-title">
            <div className="panel-heading"><h2 id="recent-tickets-title">Recent tickets</h2><span className="badge">Latest {recent.length}</span></div>
            {recent.length === 0
              ? <EmptyState icon="tickets" title="No tickets yet" description="Create a maintenance request to start tracking your work.">
                  <Link className="button button-primary" to="/tickets">Go to tickets</Link>
                </EmptyState>
              : <div className="table-scroll" role="region" aria-label="Recent tickets" tabIndex={0}>
                  <table className="ticket-table">
                    <caption className="visually-hidden">Up to five most recently created accessible tickets</caption>
                    <thead><tr><th scope="col">Ticket</th><th scope="col">Status</th><th scope="col">Priority</th><th scope="col">Created (UTC)</th></tr></thead>
                    <tbody>{recent.map((ticket) => <tr key={ticket.id}>
                      <td><span className="ticket-id">#{ticket.id}</span><Link className="ticket-title ticket-link" to={`/tickets/${ticket.id}`}>{ticket.title}</Link></td>
                      <td><span className={`status-tag status-${ticket.status}`}>{statusLabels[ticket.status]}</span></td>
                      <td><span className={`priority-tag priority-${ticket.priority}`}>{priorityLabels[ticket.priority]}</span></td>
                      <td className="nowrap"><time dateTime={utcTimestamp(ticket.createdAt)}>{formatUtcDate(ticket.createdAt)}</time></td>
                    </tr>)}</tbody>
                  </table>
                </div>}
          </section>
        </>}
      </div>
    </>
  )
}
