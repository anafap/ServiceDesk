import { TicketStatusBadge } from '../components/TicketStatusBadge'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { getTickets, priorityLabels, statusLabels } from '../api/tickets'
import type { TicketPage, TicketStatus, TicketPriority } from '../api/tickets'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { AppIcon } from '../components/AppIcon'
import { CreateTicketForm } from '../components/CreateTicketForm'
import { TicketCards } from '../components/TicketCards'
import { formatUtcDate, utcTimestamp } from '../utils/dates'
import { displayName } from '../utils/displayNames'

type TicketState =
  | { status: 'loading' }
  | { status: 'success'; result: TicketPage }
  | { status: 'error'; message: string }
export function TicketsPage() {
  const [params, setParams] = useSearchParams()
  const title = params.get('title') ?? ''
  const openOnly = params.get('openOnly') === 'true'
  const status = Object.hasOwn(statusLabels, params.get('status') ?? '')
    ? (Number(params.get('status')) as TicketStatus)
    : 'all'
  const priority = Object.hasOwn(priorityLabels, params.get('priority') ?? '')
    ? (Number(params.get('priority')) as TicketPriority)
    : 'all'
  const rawPage = Number(params.get('page') ?? 1)
  const page =
    Number.isSafeInteger(rawPage) && rawPage > 0 && rawPage <= 1000000
      ? rawPage
      : 1
  const [draft, setDraft] = useState({ title, search: title })
  if (draft.title !== title) setDraft({ title, search: title })
  const search = draft.search
  function setSearch(value: string) {
    setDraft({ title, search: value })
  }
  const [state, setState] = useState<TicketState>({ status: 'loading' })
  const [version, setVersion] = useState(0)
  const [showCreateForm, setShowCreateForm] = useState(
    params.get('create') === '1',
  )
  const [notice, setNotice] = useState('')
  const queryKey = JSON.stringify([
    title,
    status,
    priority,
    page,
    openOnly,
    version,
  ])
  const [settledKey, setSettledKey] = useState('')
  const loading = settledKey !== queryKey
  useEffect(() => {
    if (params.has('create'))
      setParams(
        (previous) => {
          const next = new URLSearchParams(previous)
          next.delete('create')
          return next
        },
        { replace: true },
      )
  }, [params, setParams])
  useEffect(() => {
    if (search === title) return
    const timer = setTimeout(() => {
      setParams(
        (previous) => {
          const next = new URLSearchParams(previous)
          if (search) next.set('title', search)
          else next.delete('title')
          next.delete('page')
          return next
        },
        { replace: true },
      )
    }, 300)
    return () => clearTimeout(timer)
  }, [search, title, setParams])
  useEffect(() => {
    const controller = new AbortController()
    getTickets(controller.signal, { title, status, priority, page, openOnly })
      .then((result) => {
        if (!controller.signal.aborted) setState({ status: 'success', result })
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setState({
            status: 'error',
            message:
              e instanceof Error && e.name !== 'TypeError'
                ? e.message
                : 'Check your connection and try again.',
          })
      })
      .finally(() => {
        if (!controller.signal.aborted) setSettledKey(queryKey)
      })
    // A changed URL immediately displays the skeleton until this request settles.
    return () => controller.abort()
  }, [title, status, priority, page, openOnly, version, queryKey])
  function change(key: string, value: string) {
    setParams((previous) => {
      const next = new URLSearchParams(previous)
      if (value === 'all' || !value) next.delete(key)
      else next.set(key, value)
      if (key !== 'page') next.delete('page')
      return next
    })
  }
  const filtered = !!title || status !== 'all' || priority !== 'all' || openOnly
  function clear() {
    setSearch('')
    setParams({})
    setVersion((v) => v + 1)
  }
  return (
    <>
      <PageHeader
        eyebrow="MAINTENANCE"
        title="Tickets"
        description="Every request, from reported issue to completed repair."
        actions={
          <button
            className="button button-primary"
            onClick={() => {
              setNotice('')
              setShowCreateForm(true)
            }}
          >
            <AppIcon name="plus" />
            New ticket
          </button>
        }
      />
      {notice && (
        <p className="form-message success-message" role="status">
          {notice}
        </p>
      )}
      {showCreateForm && (
        <CreateTicketForm
          onCancel={() => setShowCreateForm(false)}
          onCreated={(ticket) => {
            setShowCreateForm(false)
            setNotice(
              `Ticket #${ticket.id} created successfully and is awaiting approval.`,
            )
            change('page', '1')
            setVersion((v) => v + 1)
          }}
        />
      )}
      <section
        className="panel tickets-panel"
        aria-labelledby="tickets-title"
        aria-busy={loading}
      >
        <div className="panel-heading">
          <h2 id="tickets-title">
            {openOnly
              ? 'Open requests'
              : filtered
                ? 'Filtered tickets'
                : 'All tickets'}
          </h2>
          <div className="ticket-toolbar">
            {!loading && state.status === 'success' && (
              <span className="badge">{state.result.totalCount} tickets</span>
            )}
            <button
              className="button button-icon button-ghost"
              aria-label="Refresh tickets"
              disabled={loading}
              onClick={() => setVersion((v) => v + 1)}
            >
              <AppIcon name="refresh" />
            </button>
          </div>
        </div>
        <div
          className="ticket-filters"
          role="search"
          aria-label="Filter tickets"
        >
          <label>
            Search title
            <input
              type="search"
              maxLength={200}
              placeholder="Search ticket titles…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label>
            Status
            <select
              value={status}
              onChange={(e) => change('status', e.target.value)}
            >
              <option value="all">All statuses</option>
              {Object.entries(statusLabels).map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Priority
            <select
              value={priority}
              onChange={(e) => change('priority', e.target.value)}
            >
              <option value="all">All priorities</option>
              {Object.entries(priorityLabels).map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          {filtered && (
            <button className="button button-ghost" onClick={clear}>
              Clear filters
            </button>
          )}
        </div>
        {loading && (
          <div role="status" aria-label="Loading tickets">
            <div className="skeleton-row" />
            <div className="skeleton-row" />
            <div className="skeleton-row" />
          </div>
        )}
        {!loading && state.status === 'error' && (
          <div role="alert">
            <EmptyState
              icon="tickets"
              title="Tickets could not be loaded"
              description={state.message}
            >
              <button
                className="button button-secondary"
                onClick={() => setVersion((v) => v + 1)}
              >
                Try again
              </button>
            </EmptyState>
          </div>
        )}
        {!loading && state.status === 'success' && (
          <>
            {state.result.items.length === 0 ? (
              <EmptyState
                icon="tickets"
                title={
                  filtered
                    ? 'No matching tickets'
                    : page > 1
                      ? 'No tickets on this page'
                      : 'No tickets yet'
                }
                description={
                  filtered
                    ? 'Try a different title, status, or priority.'
                    : page > 1
                      ? 'Return to the first page to see the latest requests.'
                      : 'Report your first maintenance issue to start tracking repairs.'
                }
              >
                {filtered ? (
                  <button className="button button-secondary" onClick={clear}>
                    Clear filters
                  </button>
                ) : page > 1 ? (
                  <button
                    className="button button-secondary"
                    onClick={() => change('page', '1')}
                  >
                    First page
                  </button>
                ) : (
                  <button
                    className="button button-primary"
                    onClick={() => setShowCreateForm(true)}
                  >
                    New ticket
                  </button>
                )}
              </EmptyState>
            ) : (
              <>
                <div
                  className="table-scroll"
                  role="region"
                  aria-label="Ticket list"
                  tabIndex={0}
                >
                  <table className="ticket-table">
                    <thead>
                      <tr>
                        <th scope="col">Ticket</th>
                        <th scope="col">Status</th>
                        <th scope="col">Priority</th>
                        <th scope="col">Store</th>
                        <th scope="col">Created</th>
                      </tr>
                    </thead>
                    <tbody>
                      {state.result.items.map((ticket) => (
                        <tr key={ticket.id}>
                          <td>
                            <span className="ticket-id">#{ticket.id}</span>
                            <Link
                              className="ticket-title ticket-link"
                              to={`/tickets/${ticket.id}`}
                              aria-label={`View ticket #${ticket.id}: ${ticket.title}`}
                            >
                              {ticket.title}
                            </Link>
                            <p className="ticket-description">
                              {ticket.description}
                            </p>
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
                          <td>
                            {displayName(
                              ticket.storeName,
                              `Store #${ticket.storeId}`,
                            )}
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
                <TicketCards tickets={state.result.items} />
              </>
            )}
            <div className="pagination">
              <span>
                Page {page} of{' '}
                {Math.max(
                  1,
                  Math.ceil(state.result.totalCount / state.result.pageSize),
                )}{' '}
                · {state.result.totalCount} results
              </span>
              <div>
                <button
                  className="button button-secondary"
                  disabled={page <= 1}
                  onClick={() => change('page', String(page - 1))}
                >
                  Previous
                </button>
                <button
                  className="button button-secondary"
                  disabled={
                    page * state.result.pageSize >= state.result.totalCount
                  }
                  onClick={() => change('page', String(page + 1))}
                >
                  Next
                </button>
              </div>
            </div>
          </>
        )}
      </section>
    </>
  )
}
