import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { useSession } from '../auth/useSession'
import type { Ticket } from '../api/tickets'
import { statusLabels } from '../api/tickets'
import { getTicketHistory } from '../api/ticketDetails'
import type { TicketHistoryEntry } from '../api/ticketDetails'
import { apiFetch } from '../api/http'
import { problemMessage } from '../api/problems'

export function TicketCorrection(props: { ticket: Ticket; onUpdated: (message: string) => void; busy: boolean; onBusyChange: (busy: boolean) => void }) {
  const session = useSession()
  return session?.user.role === 'Admin' ? <CorrectionForm {...props} /> : null
}
function CorrectionForm({ ticket, onUpdated, busy, onBusyChange }: { ticket: Ticket; onUpdated: (message: string) => void; busy: boolean; onBusyChange: (busy: boolean) => void }) {
  const [event, setEvent] = useState<TicketHistoryEntry | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [version, setVersion] = useState(0)
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    getTicketHistory(ticket.id, controller.signal).then(entries => { if (!controller.signal.aborted) { setEvent(entries.at(-1) ?? null); setLoading(false) } })
      .catch(() => { if (!controller.signal.aborted) { setError('Previous status could not be loaded.'); setLoading(false) } })
    return () => controller.abort()
  }, [ticket.id, version])
  async function submit(submission: SubmitEvent<HTMLFormElement>) {
    submission.preventDefault()
    if (pending.current || !event || event.previousStatus === null) return
    if (!reason.trim() || reason.trim().length > 2000) { setError('A reason of at most 2,000 characters is required.'); return }
    const controller = new AbortController(); pending.current = controller; setSaving(true); onBusyChange(true); setError('')
    try {
      const response = await apiFetch(`/api/tickets/${ticket.id}/correct-status`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ eventId: event.id, reason: reason.trim(), expectedVersion: ticket.version }),
        signal: AbortSignal.any([controller.signal, AbortSignal.timeout(15_000)]),
      })
      if (!response.ok) throw new Error(await problemMessage(response, 'Correction was not accepted. Refresh ticket details before trying again.'))
      if (!controller.signal.aborted) onUpdated('Status corrected. The original event and correction remain in history.')
    } catch (failure: unknown) {
      if (!controller.signal.aborted) setError(failure instanceof Error && failure.name === 'Error' ? failure.message : 'We could not confirm the correction. Refresh ticket details before trying again.')
    } finally { pending.current = null; if (!controller.signal.aborted) setSaving(false); onBusyChange(false) }
  }
  const canCorrect = event?.previousStatus !== null && event?.newStatus === ticket.status
  return <section className="panel ticket-details"><div className="panel-heading"><h2>Correct a status mistake</h2></div><div className="detail-body">
    {loading ? <p role="status">Loading previous event…</p> : !canCorrect ? <p>No recorded previous state is available to correct.</p> : !open ? <button className="button button-secondary" disabled={busy} onClick={() => setOpen(true)}>Correct status</button> : <form onSubmit={submit}>
      <p>Return <strong>{statusLabels[ticket.status]}</strong> to <strong>{statusLabels[event!.previousStatus!]}</strong> by reversing event #{event!.id}. The original event will remain in history.</p>
      <label className="form-field">Correction reason<textarea required maxLength={2000} value={reason} disabled={saving} onChange={change => setReason(change.target.value)} /></label>
      <div className="page-actions"><button className="button button-primary" disabled={busy}>{saving ? 'Correcting…' : 'Confirm correction'}</button><button className="button button-secondary" type="button" disabled={saving} onClick={() => setOpen(false)}>Cancel</button></div>
    </form>}
    {error && <div role="alert"><p className="form-message error-message">{error}</p>{!open && <button className="button button-secondary" onClick={() => { setLoading(true); setError(''); setVersion(value => value + 1) }}>Try again</button>}</div>}
  </div></section>
}
