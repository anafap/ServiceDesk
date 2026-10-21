import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { getReferenceData } from '../api/referenceData'
import type { ReferenceItem } from '../api/referenceData'
import { createTicket, priorityLabels } from '../api/tickets'
import type { Ticket, TicketPriority } from '../api/tickets'
import { getDemoCreatorId, validateTicketForm } from '../forms/ticketForm'
import type { TicketFormErrors, TicketFormValues } from '../forms/ticketForm'

type LookupState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'success'; stores: ReferenceItem[]; categories: ReferenceItem[] }

type CreateTicketFormProps = {
  onCreated: (ticket: Ticket) => void
  onCancel: () => void
}

const demoCreatorId = getDemoCreatorId(import.meta.env.VITE_DEMO_USER_ID, import.meta.env.DEV)

export function CreateTicketForm({ onCreated, onCancel }: CreateTicketFormProps) {
  const [lookups, setLookups] = useState<LookupState>({ status: 'loading' })
  const [lookupVersion, setLookupVersion] = useState(0)
  const [values, setValues] = useState<TicketFormValues>({ title: '', description: '', storeId: '', categoryId: '', priority: '2' })
  const [errors, setErrors] = useState<TicketFormErrors>({})
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState('')
  const activeSave = useRef<AbortController | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    heading.current?.focus()
    return () => activeSave.current?.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    getReferenceData(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setLookups({ status: 'success', ...data })
      })
      .catch(() => {
        if (!controller.signal.aborted) setLookups({ status: 'error' })
      })
    return () => controller.abort()
  }, [lookupVersion])

  function update(field: keyof TicketFormValues, value: string) {
    setValues((previous) => ({ ...previous, [field]: value }))
    setErrors((previous) => ({ ...previous, [field]: undefined }))
  }

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (activeSave.current || lookups.status !== 'success' || demoCreatorId === null) return
    const validationErrors = validateTicketForm(values, lookups.stores, lookups.categories)
    setErrors(validationErrors)
    setSaveError('')
    if (Object.keys(validationErrors).length > 0) {
      const firstField = Object.keys(validationErrors)[0]
      const field = event.currentTarget.elements.namedItem(firstField)
      if (field instanceof HTMLElement) field.focus()
      return
    }

    const controller = new AbortController()
    activeSave.current = controller
    setSaving(true)
    try {
      const ticket = await createTicket({
        title: values.title.trim(), description: values.description.trim(),
        storeId: Number(values.storeId), categoryId: Number(values.categoryId),
        priority: Number(values.priority) as TicketPriority, createdByUserId: demoCreatorId,
      }, controller.signal)
      if (!controller.signal.aborted) onCreated(ticket)
    } catch (error: unknown) {
      if (!controller.signal.aborted) setSaveError(error instanceof Error ? error.message : 'The ticket could not be saved.')
    } finally {
      activeSave.current = null
      if (!controller.signal.aborted) setSaving(false)
    }
  }

  const canSubmit = demoCreatorId !== null && lookups.status === 'success'
    && lookups.stores.length > 0 && lookups.categories.length > 0

  return (
    <section id="create-ticket" className="panel create-ticket" aria-labelledby="create-ticket-title">
      <div className="panel-heading"><h2 id="create-ticket-title" ref={heading} tabIndex={-1}>Create a ticket</h2><span className="badge">Local demo</span></div>
      <div className="ticket-form-content">
        <p className="demo-notice">{demoCreatorId === null
          ? 'Ticket creation is unavailable in this demo. A demo creator must be configured by the workspace owner.'
          : `Local demo: requests are recorded as demo user #${demoCreatorId}. Sign-in is not available yet.`}</p>
        {lookups.status === 'loading' && <p role="status" className="lookup-message">Loading stores and categories…</p>}
        {lookups.status === 'error' && <div className="form-message error-message" role="alert">
          <p>Stores and categories could not be loaded.</p>
          <button className="button button-secondary" type="button" onClick={() => { setLookups({ status: 'loading' }); setLookupVersion((version) => version + 1) }}>Try again</button>
        </div>}
        {lookups.status === 'success' && (lookups.stores.length === 0 || lookups.categories.length === 0) && <p className="form-message" role="status">A store and category must be available before a ticket can be created.</p>}
        <form noValidate onSubmit={submit} aria-busy={saving}>
          <fieldset disabled={!canSubmit || saving} className="ticket-fields">
            <legend className="visually-hidden">Ticket information. All fields are required.</legend>
            <div className="form-field field-full">
              <label htmlFor="ticket-title">Title <span>Required</span></label>
              <input id="ticket-title" name="title" value={values.title} onChange={(event) => update('title', event.target.value)} maxLength={200} required aria-invalid={Boolean(errors.title)} aria-describedby={errors.title ? 'title-error' : undefined} />
              {errors.title && <p id="title-error" className="field-error">{errors.title}</p>}
            </div>
            <div className="form-field field-full">
              <label htmlFor="ticket-description">Description <span>Required</span></label>
              <textarea id="ticket-description" name="description" value={values.description} onChange={(event) => update('description', event.target.value)} maxLength={4000} rows={4} required aria-invalid={Boolean(errors.description)} aria-describedby={errors.description ? 'description-error' : undefined} />
              {errors.description && <p id="description-error" className="field-error">{errors.description}</p>}
            </div>
            <div className="form-field">
              <label htmlFor="ticket-store">Store <span>Required</span></label>
              <select id="ticket-store" name="storeId" value={values.storeId} onChange={(event) => update('storeId', event.target.value)} required aria-invalid={Boolean(errors.storeId)} aria-describedby={errors.storeId ? 'store-error' : undefined}>
                <option value="">Select a store</option>
                {lookups.status === 'success' && lookups.stores.map((store) => <option key={store.id} value={store.id}>{store.name}</option>)}
              </select>
              {errors.storeId && <p id="store-error" className="field-error">{errors.storeId}</p>}
            </div>
            <div className="form-field">
              <label htmlFor="ticket-category">Category <span>Required</span></label>
              <select id="ticket-category" name="categoryId" value={values.categoryId} onChange={(event) => update('categoryId', event.target.value)} required aria-invalid={Boolean(errors.categoryId)} aria-describedby={errors.categoryId ? 'category-error' : undefined}>
                <option value="">Select a category</option>
                {lookups.status === 'success' && lookups.categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
              </select>
              {errors.categoryId && <p id="category-error" className="field-error">{errors.categoryId}</p>}
            </div>
            <div className="form-field">
              <label htmlFor="ticket-priority">Priority <span>Required</span></label>
              <select id="ticket-priority" name="priority" value={values.priority} onChange={(event) => update('priority', event.target.value)} required aria-invalid={Boolean(errors.priority)} aria-describedby={errors.priority ? 'priority-error' : undefined}>
                {Object.entries(priorityLabels).map(([id, name]) => <option key={id} value={id}>{name}</option>)}
              </select>
              {errors.priority && <p id="priority-error" className="field-error">{errors.priority}</p>}
            </div>
          </fieldset>
          {saveError && <p className="form-message error-message" role="alert">{saveError}</p>}
          <div className="form-actions">
            <button className="button button-secondary" type="button" disabled={saving} onClick={onCancel}>Cancel</button>
            <button className="button button-primary" type="submit" disabled={!canSubmit || saving}>{saving ? 'Saving…' : 'Create ticket'}</button>
          </div>
          {saving && <p className="visually-hidden" role="status">Saving your ticket…</p>}
        </form>
      </div>
    </section>
  )
}
