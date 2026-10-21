import { useEffect, useState } from 'react'
import { getTickets, priorityLabels, statusLabels } from '../api/tickets'
import type { Ticket } from '../api/tickets'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { CreateTicketForm } from '../components/CreateTicketForm'

type TicketState =
  | { status: 'loading' }
  | { status: 'success'; tickets: Ticket[] }
  | { status: 'error'; message: string }

const dateFormat = new Intl.DateTimeFormat(undefined, {
  year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC',
})

function formatCreatedAt(value: string) {
  // SQL Server may return our stored UTC timestamps without a timezone suffix.
  const utcValue = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
  return dateFormat.format(new Date(utcValue))
}

export function TicketsPage() {
  const [state, setState] = useState<TicketState>({ status: 'loading' })
  const [requestVersion, setRequestVersion] = useState(0)
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [notice, setNotice] = useState('')

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

  function reloadTickets() {
    setState({ status: 'loading' })
    setRequestVersion((version) => version + 1)
  }

  return (
    <>
      <PageHeader eyebrow="MAINTENANCE" title="Tickets" description="Track requests, coordinate assignments, and follow each resolution." />
      <div className="page-actions">
        <button className="button button-primary" type="button" aria-expanded={showCreateForm} aria-controls="create-ticket" disabled={showCreateForm} onClick={() => { setNotice(''); setShowCreateForm(true) }}>New ticket</button>
      </div>
      {notice && <p className="form-message success-message" role="status">{notice}</p>}
      {showCreateForm && <CreateTicketForm
        onCancel={() => setShowCreateForm(false)}
        onCreated={(ticket) => {
          setShowCreateForm(false)
          setNotice(`Ticket #${ticket.id} created successfully and is awaiting approval.`)
          reloadTickets()
        }}
      />}
      <section className="panel" aria-labelledby="tickets-title" aria-busy={state.status === 'loading'}>
        <div className="panel-heading">
          <h2 id="tickets-title">All tickets</h2>
          <div className="ticket-toolbar">
            {state.status === 'success' && <span className="badge">{state.tickets.length} {state.tickets.length === 1 ? 'ticket' : 'tickets'}</span>}
            <button className="button button-secondary" type="button" disabled={state.status === 'loading'} onClick={reloadTickets}>Refresh</button>
          </div>
        </div>
        {state.status === 'loading' && <p className="loading-state" role="status">Loading tickets…</p>}
        {state.status === 'error' && (
          <div role="alert">
            <EmptyState icon="tickets" title="We couldn't load your tickets" description={state.message}>
              <button className="button button-primary" type="button" onClick={reloadTickets}>Try again</button>
            </EmptyState>
          </div>
        )}
        {state.status === 'success' && (state.tickets.length === 0
          ? <EmptyState icon="tickets" title="No tickets yet" description="There are no maintenance requests to display." />
          : <div className="table-scroll" role="region" aria-label="Ticket list" tabIndex={0}>
              <table className="ticket-table">
                <caption className="visually-hidden">Maintenance tickets from the ticket service</caption>
                <thead><tr><th scope="col">Ticket</th><th scope="col">Status</th><th scope="col">Priority</th><th scope="col">Store</th><th scope="col">Created (UTC)</th></tr></thead>
                <tbody>{state.tickets.map((ticket) => (
                  <tr key={ticket.id}>
                    <td><span className="ticket-id">#{ticket.id}</span><strong className="ticket-title">{ticket.title}</strong><p className="ticket-description">{ticket.description}</p></td>
                    <td><span className={`status-tag status-${ticket.status}`}>{statusLabels[ticket.status]}</span></td>
                    <td><span className={`priority-tag priority-${ticket.priority}`}>{priorityLabels[ticket.priority]}</span></td>
                    <td className="nowrap">Store #{ticket.storeId}</td>
                    <td className="nowrap"><time dateTime={ticket.createdAt}>{formatCreatedAt(ticket.createdAt)}</time></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
        )}
      </section>
    </>
  )
}
