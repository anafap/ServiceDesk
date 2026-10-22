import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router'
import { listAdminRecords, resourceFields, saveAdminRecord, validateAdminInput } from '../api/administration'
import type { AdminRecord, ResourceKind } from '../api/administration'
import { useSession } from '../auth/useSession'
import { PageHeader } from '../components/PageHeader'

export function AdministrationPage({ kind }: { kind: ResourceKind }) {
  const session = useSession()
  if (session?.user.role !== 'Admin') return <section className="panel detail-body"><h1>Administrator access required</h1><Link to="/">Return to overview</Link></section>
  return <AdminWorkspace key={kind} kind={kind} />
}

function AdminWorkspace({ kind }: { kind: ResourceKind }) {
  const [records, setRecords] = useState<AdminRecord[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [version, setVersion] = useState(0)
  const [editing, setEditing] = useState<{ id: number | null; values: Record<string, string> } | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saveError, setSaveError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [activation, setActivation] = useState<AdminRecord | null>(null)
  const writeController = useRef<AbortController | null>(null)
  const writing = useRef(false)
  const fields = resourceFields[kind]
  const title = kind === 'stores' ? 'Stores' : 'Technicians'
  useEffect(() => () => writeController.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    listAdminRecords(kind, controller.signal).then(items => { if (!controller.signal.aborted) { setRecords(items); setLoading(false) } })
      .catch((error: unknown) => { if (!controller.signal.aborted) { setLoadError(error instanceof Error ? error.message : 'Unable to load records.'); setLoading(false) } })
    return () => controller.abort()
  }, [kind, version])
  function reload() { setLoading(true); setLoadError(''); setVersion(value => value + 1) }
  function startEdit(record: AdminRecord | null) {
    setActivation(null); setErrors({}); setSaveError(''); setNotice('')
    setEditing({ id: record?.id ?? null, values: Object.fromEntries(fields.map(field => [field.key, record ? String(record[field.key]) : ''])) })
  }
  async function write(perform: (signal: AbortSignal) => Promise<void>) {
    if (writing.current) return
    writing.current = true; setBusy(true); setSaveError(''); setNotice('')
    const controller = new AbortController(); writeController.current = controller
    try { await perform(controller.signal); if (!controller.signal.aborted) { setEditing(null); setActivation(null); setNotice('Changes saved.'); reload() } }
    catch (error: unknown) { if (!controller.signal.aborted) setSaveError(error instanceof Error ? error.message : 'Unable to save.') }
    finally { writing.current = false; if (!controller.signal.aborted) setBusy(false) }
  }
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!editing) return
    const validation = validateAdminInput(kind, editing.values); setErrors(validation)
    if (Object.keys(validation).length) return
    void write(signal => saveAdminRecord(kind, editing.id, Object.fromEntries(Object.entries(editing.values).map(([key, value]) => [key, value.trim()])), signal))
  }
  return <>
    <PageHeader eyebrow="ADMINISTRATION" title={title} description={`Manage ${title.toLowerCase()} and preserve records used by maintenance history.`} />
    <div className="page-actions"><button className="button button-primary" type="button" disabled={busy || !!editing || !!activation} onClick={() => startEdit(null)}>Add {kind === 'stores' ? 'store' : 'technician'}</button>
      <button className="button button-secondary" type="button" disabled={busy || loading} onClick={reload}>Refresh</button></div>
    {notice && <p className="form-message success-message" role="status">{notice}</p>}
    {saveError && <p className="form-message error-message" role="alert">{saveError}</p>}
    {editing && <section className="panel detail-body"><h2>{editing.id === null ? 'Add record' : 'Edit record'}</h2><form onSubmit={submit} noValidate>
      <div className="ticket-fields">{fields.map(field => <label key={field.key}>{field.label}<input required maxLength={field.maxLength} disabled={busy} value={editing.values[field.key]} aria-invalid={!!errors[field.key]} aria-describedby={errors[field.key] ? `error-${field.key}` : undefined} onChange={event => setEditing({ ...editing, values: { ...editing.values, [field.key]: event.target.value } })} />
        {errors[field.key] && <span className="field-error" id={`error-${field.key}`}>{errors[field.key]}</span>}</label>)}</div>
      <div className="page-actions"><button className="button button-primary" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button><button className="button button-secondary" type="button" disabled={busy} onClick={() => { setEditing(null); setSaveError('') }}>Cancel</button></div>
    </form></section>}
    {activation && <section className="panel detail-body" aria-labelledby="activation-heading"><h2 id="activation-heading">{activation.isActive ? 'Deactivate' : 'Reactivate'} {String(activation.name ?? activation.fullName)}?</h2>
      <p>{activation.isActive ? 'Existing tickets and history remain available. This record will no longer be offered for new work.' : 'This record will be available for new work again.'}</p>
      <div className="page-actions"><button className="button button-primary" disabled={busy} onClick={() => void write(signal => saveAdminRecord(kind, activation.id, { isActive: !activation.isActive }, signal, true))}>Confirm</button><button className="button button-secondary" disabled={busy} onClick={() => { setActivation(null); setSaveError('') }}>Cancel</button></div></section>}
    <section className="panel" aria-busy={loading}><div className="panel-heading"><h2>All {title.toLowerCase()}</h2></div>
      {loading ? <p className="loading-state" role="status">Loading…</p> : loadError ? <div className="detail-body" role="alert"><p>{loadError}</p><button className="button button-secondary" onClick={reload}>Try again</button></div> : records.length === 0 ? <p className="detail-body">No records yet. Add one to get started.</p> : <div className="table-scroll" role="region" aria-label={title} tabIndex={0}><table className="ticket-table"><thead><tr>{fields.map(field => <th key={field.key} scope="col">{field.label}</th>)}<th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody>{records.map(record => <tr key={record.id}>{fields.map(field => <td key={field.key}>{String(record[field.key])}</td>)}<td>{record.isActive ? 'Active' : 'Inactive'}</td><td><div className="workflow-buttons"><button className="button button-secondary" disabled={busy || !!editing || !!activation} onClick={() => startEdit(record)}>Edit<span className="visually-hidden"> {String(record.name ?? record.fullName)}</span></button><button className="button button-secondary" disabled={busy || !!editing || !!activation} onClick={() => { setActivation(record); setSaveError(''); setNotice('') }}>{record.isActive ? 'Deactivate' : 'Reactivate'}<span className="visually-hidden"> {String(record.name ?? record.fullName)}</span></button></div></td></tr>)}</tbody></table></div>}
    </section>
  </>
}
