import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { editTicket } from '../api/editTicket'
import type { TicketDetails } from '../api/ticketDetails'
import { priorityLabels } from '../api/tickets'
import type { TicketPriority } from '../api/tickets'
import { validateTicketDetails } from '../forms/ticketForm'
import type { EditTicketValues, TicketFormErrors } from '../forms/ticketForm'

export function EditTicketForm({ ticket, onSaved, onCancel }: {
  ticket: TicketDetails; onSaved: () => void; onCancel: () => void
}) {
  const [values, setValues] = useState<EditTicketValues>({ title: ticket.title, description: ticket.description, priority: String(ticket.priority) })
  const [errors, setErrors] = useState<TicketFormErrors>({})
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef<AbortController | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus(); return () => pending.current?.abort() }, [])

  function update(field: keyof EditTicketValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
  }

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending.current) return
    const errors = validateTicketDetails(values)
    setErrors(errors)
    setMessage('')
    if (Object.keys(errors).length) {
      const field = event.currentTarget.elements.namedItem(Object.keys(errors)[0])
      if (field instanceof HTMLElement) field.focus()
      return
    }
    const controller = new AbortController()
    pending.current = controller
    setSaving(true)
    try {
      await editTicket(ticket.id, { title: values.title.trim(), description: values.description.trim(), priority: Number(values.priority) as TicketPriority }, controller.signal)
      if (!controller.signal.aborted) onSaved()
    } catch (error: unknown) {
      if (!controller.signal.aborted) setMessage(error instanceof Error ? error.message : 'Changes could not be saved.')
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setSaving(false)
    }
  }

  return <section className="panel create-ticket" aria-labelledby="edit-title">
    <div className="panel-heading"><h2 id="edit-title" tabIndex={-1} ref={heading}>Edit ticket</h2></div>
    <form className="ticket-form-content" noValidate onSubmit={submit} aria-busy={saving}>
      <fieldset className="ticket-fields" disabled={saving}>
        <legend className="visually-hidden">Edit ticket information</legend>
        <div className="form-field field-full"><label htmlFor="edit-ticket-title">Title</label>
          <input id="edit-ticket-title" name="title" value={values.title} maxLength={200} required onChange={(event) => update('title', event.target.value)} aria-invalid={Boolean(errors.title)} aria-describedby={errors.title ? 'edit-title-error' : undefined} />
          {errors.title && <p className="field-error" id="edit-title-error">{errors.title}</p>}</div>
        <div className="form-field field-full"><label htmlFor="edit-ticket-description">Description</label>
          <textarea id="edit-ticket-description" name="description" value={values.description} maxLength={4000} rows={5} required onChange={(event) => update('description', event.target.value)} aria-invalid={Boolean(errors.description)} aria-describedby={errors.description ? 'edit-description-error' : undefined} />
          {errors.description && <p className="field-error" id="edit-description-error">{errors.description}</p>}</div>
        <div className="form-field"><label htmlFor="edit-ticket-priority">Priority</label>
          <select id="edit-ticket-priority" name="priority" value={values.priority} required onChange={(event) => update('priority', event.target.value)} aria-invalid={Boolean(errors.priority)} aria-describedby={errors.priority ? 'edit-priority-error' : undefined}>
            {Object.entries(priorityLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}
          </select>{errors.priority && <p className="field-error" id="edit-priority-error">{errors.priority}</p>}</div>
      </fieldset>
      {message && <p role="alert" className="form-message error-message">{message}</p>}
      <div className="form-actions"><button type="button" className="button button-secondary" disabled={saving} onClick={onCancel}>Cancel</button>
        <button type="submit" className="button button-primary" disabled={saving}>{saving ? 'Saving…' : 'Save changes'}</button></div>
    </form>
  </section>
}
