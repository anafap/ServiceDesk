import { TicketStatusBadge } from '../components/TicketStatusBadge'
import { AppIcon } from '../components/AppIcon'
import { TicketCards } from '../components/TicketCards'
import { useSession } from '../auth/useSession'
import { displayName } from '../utils/displayNames'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { getDashboard, priorityLabels, ticketStatusLabel } from '../api/tickets'
import type { Dashboard } from '../api/tickets'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { formatUtcDate, utcTimestamp } from '../utils/dates'

type OverviewState =
  | { status: 'loading' }
  | { status: 'success'; dashboard: Dashboard }
  | { status: 'error'; message: string }

export function OverviewPage() {
  const session = useSession()
  const [state, setState] = useState<OverviewState>({ status: 'loading' })
  const [requestVersion, setRequestVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getDashboard(controller.signal)
      .then((dashboard) => {
        if (!controller.signal.aborted)
          setState({ status: 'success', dashboard })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        const message =
          error instanceof Error && error.name === 'TimeoutError'
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

  const summary = state.status === 'success' ? state.dashboard.summary : null
  const recent = state.status === 'success' ? state.dashboard.recent : []

  return (
    <>
      <PageHeader
        eyebrow="OPERATIONS AT A GLANCE"
        title={
          session?.user.role === 'StoreManager'
            ? 'My store overview'
            : 'Workspace overview'
        }
        description={
          session?.user.role === 'StoreManager'
            ? `Keep maintenance moving at ${displayName(session.user.storeName, `Store #${session.user.storeId}`)}.`
            : 'A clear view of maintenance across your stores.'
        }
        actions={
          <>
            <button
              className="button button-icon button-secondary"
              aria-label="Refresh dashboard"
              disabled={state.status === 'loading'}
              onClick={refresh}
            >
              <AppIcon name="refresh" />
            </button>
            <Link className="button button-primary" to="/tickets?create=1">
              <AppIcon name="plus" />
              New ticket
            </Link>
          </>
        }
      />
      <div aria-busy={state.status === 'loading'}>
        {state.status === 'loading' && (
          <div
            className="skeleton-block"
            role="status"
            aria-label="Loading dashboard"
          />
        )}
        {state.status === 'error' && (
          <section className="panel" role="alert">
            <EmptyState
              icon="overview"
              title="We couldn't load your dashboard"
              description={state.message}
            >
              <button
                className="button button-primary"
                type="button"
                onClick={refresh}
              >
                Try again
              </button>
            </EmptyState>
          </section>
        )}
        {summary && (
          <>
            <section aria-label="Ticket summaries">
              <div className="summary-grid">
                {[
                  {
                    label: 'Total tickets',
                    count: summary.total,
                    hint: 'Across your workspace',
                    query: '',
                  },
                  {
                    label: 'Open requests',
                    count: summary.open,
                    hint: 'Approval, assignment & visits',
                    query: '?openOnly=true',
                  },
                  {
                    label: 'In progress',
                    count: summary.inProgress,
                    hint: 'Work is underway',
                    query: '?status=4',
                  },
                  {
                    label: 'Awaiting closure',
                    count: summary.resolved,
                    hint: 'Repair marked as resolved',
                    query: '?status=5',
                  },
                ].map((card) => (
                  <Link
                    className="panel summary-card"
                    key={card.label}
                    to={`/tickets${card.query}`}
                  >
                    <span className="summary-label">
                      {card.label}
                      <AppIcon name="arrow" />
                    </span>
                    <strong className="summary-number">{card.count}</strong>
                    <p>{card.hint}</p>
                  </Link>
                ))}
              </div>
            </section>
            <div className="overview-columns">
              <section className="panel" aria-labelledby="attention-title">
                <div className="panel-heading">
                  <div>
                    <h2 id="attention-title">Needs attention</h2>
                    <p>
                      {session?.user.role === 'Admin'
                        ? 'Requests ready for your next decision.'
                        : 'Assigned repairs and work in progress.'}
                    </p>
                  </div>
                  <span className="badge">
                    {state.status === 'success'
                      ? state.dashboard.attention.length
                      : 0}
                  </span>
                </div>
                {state.status === 'success' &&
                  (state.dashboard.attention.length === 0 ? (
                    <EmptyState
                      icon="overview"
                      title="You're all caught up"
                      description="Requests that need your next action will appear here."
                    />
                  ) : (
                    <ul className="attention-list">
                      {state.dashboard.attention.map(({ ticket, action }) => (
                        <li key={ticket.id}>
                          <Link to={`/tickets/${ticket.id}`}>
                            <div>
                              <span className="ticket-id">
                                #{ticket.id} ·{' '}
                                {ticket.storeName || `Store #${ticket.storeId}`}
                              </span>
                              <span className="attention-title">
                                {ticket.title}
                              </span>
                              <span className="attention-meta">
                                <span
                                  className={`priority-tag priority-${ticket.priority}`}
                                >
                                  {priorityLabels[ticket.priority]}
                                </span>
                                <span>{ticketStatusLabel(ticket)}</span>
                              </span>
                            </div>
                            <span className="attention-action">
                              {action}
                              <AppIcon name="arrow" />
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  ))}
              </section>
              <section className="panel" aria-labelledby="recent-tickets-title">
                <div className="panel-heading">
                  <h2 id="recent-tickets-title">Recent tickets</h2>
                  <span className="badge">Latest {recent.length}</span>
                </div>
                {recent.length === 0 ? (
                  <EmptyState
                    icon="tickets"
                    title="No tickets yet"
                    description="Create a maintenance request to start tracking your work."
                  >
                    <Link className="button button-primary" to="/tickets">
                      Go to tickets
                    </Link>
                  </EmptyState>
                ) : (
                  <div
                    className="table-scroll"
                    role="region"
                    aria-label="Recent tickets"
                    tabIndex={0}
                  >
                    <table className="ticket-table">
                      <caption className="visually-hidden">
                        Up to five most recently created accessible tickets
                      </caption>
                      <thead>
                        <tr>
                          <th scope="col">Ticket</th>
                          <th scope="col">Status</th>
                          <th scope="col">Priority</th>
                          <th scope="col">Created</th>
                        </tr>
                      </thead>
                      <tbody>
                        {recent.map((ticket) => (
                          <tr key={ticket.id}>
                            <td>
                              <span className="ticket-id">#{ticket.id}</span>
                              <Link
                                className="ticket-title ticket-link"
                                to={`/tickets/${ticket.id}`}
                              >
                                {ticket.title}
                              </Link>
                            </td>
                            <td>
                              <TicketStatusBadge ticket={ticket} />
                            </td>
                            <td>
                              <span
                                className={`priority-tag priority-${ticket.priority}`}
                              >
                                {priorityLabels[ticket.priority]}
                              </span>
                            </td>
                            <td className="nowrap">
                              <time dateTime={utcTimestamp(ticket.createdAt)}>
                                {formatUtcDate(ticket.createdAt)}
                              </time>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                <TicketCards tickets={recent} />
              </section>
            </div>
          </>
        )}
      </div>
    </>
  )
}
