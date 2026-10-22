import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { Link } from 'react-router'
import { listAdminRecords, resetManagerPassword, resourceFields, saveAdminRecord, validateAdminInput } from '../api/administration'
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
  const [stores, setStores] = useState<AdminRecord[]>([])
  const [storeError, setStoreError] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [version, setVersion] = useState(0)
  const [editing, setEditing] = useState<{ id: number | null; version: string; values: Record<string, string> } | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saveError, setSaveError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [activation, setActivation] = useState<AdminRecord | null>(null)
  const [resetting, setResetting] = useState<AdminRecord | null>(null)
  const [password, setPassword] = useState('')
  const writeController = useRef<AbortController | null>(null)
  const writing = useRef(false)
  const fields = resourceFields[kind]
  const title = kind === 'stores' ? 'Stores' : kind === 'store-managers' ? 'StoreManagers' : 'Technicians'
  const formOpen = !!editing || !!activation || !!resetting
  useEffect(() => () => writeController.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    listAdminRecords(kind, controller.signal)
      .then(items => { if (!controller.signal.aborted) { setRecords(items); setLoading(false) } })
      .catch((error: unknown) => { if (!controller.signal.aborted) { setLoadError(error instanceof Error ? error.message : 'Unable to load records.'); setLoading(false) } })
    if (kind === 'store-managers') listAdminRecords('stores', controller.signal)
      .then(items => { if (!controller.signal.aborted) { setStores(items); setStoreError('') } })
      .catch(() => { if (!controller.signal.aborted) setStoreError('Store choices could not be loaded. Refresh to try again.') })
    return () => controller.abort()
  }, [kind, version])
  function reload() { setLoading(true); setLoadError(''); setVersion(value => value + 1) }
  function cancel() { setEditing(null); setActivation(null); setResetting(null); setPassword(''); setSaveError(''); setErrors({}) }
  function startEdit(record: AdminRecord | null) {
    cancel(); setNotice('')
    setEditing({ id: record?.id ?? null, version: record?.version ?? '', values: Object.fromEntries(fields.map(field => [field.key, record ? String(record[field.key]) : ''])) })
  }
  async function performWrite(perform: (signal: AbortSignal) => Promise<void>) {
    if (writing.current) return
    writing.current = true; setBusy(true); setSaveError(''); setNotice('')
    const controller = new AbortController(); writeController.current = controller
    try {
      await perform(controller.signal)
      if (!controller.signal.aborted) { cancel(); setNotice(kind === 'store-managers' ? 'Changes saved. Access changes require the manager to sign in again.' : 'Changes saved.'); reload() }
    } catch (error: unknown) { if (!controller.signal.aborted) setSaveError(error instanceof Error ? error.message : 'Unable to save.') }
    finally { writing.current = false; if (!controller.signal.aborted) setBusy(false) }
  }
  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault(); if (!editing) return
    const validation = validateAdminInput(kind, editing.values)
    if (kind === 'store-managers' && editing.id === null && (password.length < 8 || password.length > 128)) validation.password = 'Password must contain 8 to 128 characters.'
    setErrors(validation)
    if (Object.keys(validation).length) return
    const input: Record<string, string | number | boolean> = Object.fromEntries(Object.entries(editing.values).map(([key, value]) => [key, value.trim()]))
    if (kind === 'store-managers') { input.storeId = Number(editing.values.storeId); if (editing.id === null) input.password = password }
    if (editing.id !== null) input.expectedVersion = editing.version
    void performWrite(signal => saveAdminRecord(kind, editing.id, input, signal))
  }
  function recordName(record: AdminRecord) { return String(record.name ?? record.fullName ?? `${record.firstName} ${record.lastName}`) }
  return <>
    <PageHeader eyebrow="ADMINISTRATION" title={title} description={`Manage ${title.toLowerCase()} and preserve records used by maintenance history.`} />
    <div className="page-actions">
      <button className="button button-primary" type="button" disabled={busy || formOpen} onClick={() => startEdit(null)}>Add {kind === 'stores' ? 'store' : kind === 'store-managers' ? 'StoreManager' : 'technician'}</button>
      <button className="button button-secondary" type="button" disabled={busy || loading} onClick={reload}>Refresh</button>
    </div>
    {notice && <p className="form-message success-message" role="status">{notice}</p>}
    {saveError && <p className="form-message error-message" role="alert">{saveError}</p>}
    {storeError && <p className="form-message error-message" role="alert">{storeError}</p>}
    {editing && <section className="panel detail-body"><h2>{editing.id === null ? 'Add record' : 'Edit record'}</h2>
      <form onSubmit={submit} noValidate><fieldset className="ticket-fields" disabled={busy}>
        <legend className="visually-hidden">Record details</legend>
        {fields.map(field => <label key={field.key}>{field.label}
          {field.type === 'store' ? <select required value={editing.values[field.key]} onChange={event => setEditing({ ...editing, values: { ...editing.values, [field.key]: event.target.value } })}>
            <option value="">Select an active store</option>
            {stores.filter(store => store.isActive || String(store.id) === editing.values.storeId).map(store => <option key={store.id} value={store.id}>{String(store.name)}{store.isActive ? '' : ' (inactive; existing assignment)'}</option>)}
          </select> : <input required type={field.type ?? 'text'} maxLength={field.maxLength} value={editing.values[field.key]}
            aria-invalid={!!errors[field.key]} aria-describedby={errors[field.key] ? `error-${field.key}` : undefined}
            onChange={event => setEditing({ ...editing, values: { ...editing.values, [field.key]: event.target.value } })} />}
          {errors[field.key] && <span className="field-error" id={`error-${field.key}`}>{errors[field.key]}</span>}
        </label>)}
        {kind === 'store-managers' && editing.id === null && <label>Initial password<input type="password" autoComplete="new-password" minLength={8} maxLength={128} required value={password} onChange={event => setPassword(event.target.value)} />{errors.password && <span className="field-error">{errors.password}</span>}</label>}
      </fieldset><div className="page-actions"><button className="button button-primary" disabled={busy || (kind === 'store-managers' && !!storeError)}>{busy ? 'Saving…' : 'Save'}</button><button className="button button-secondary" type="button" disabled={busy} onClick={cancel}>Cancel</button></div></form>
    </section>}
    {activation && <section className="panel detail-body" aria-labelledby="activation-heading">
      <h2 id="activation-heading">{activation.isActive ? 'Deactivate' : 'Reactivate'} {recordName(activation)}?</h2>
      <p>{kind === 'store-managers' ? 'Changing account access invalidates previous sessions. Historical tickets and audit records remain.' : activation.isActive ? 'Existing tickets and history remain available. This record will no longer be offered for new work.' : 'This record will be available for new work again.'}</p>
      <div className="page-actions"><button className="button button-primary" disabled={busy} onClick={() => void performWrite(signal => saveAdminRecord(kind, activation.id, { isActive: !activation.isActive, expectedVersion: activation.version }, signal, true))}>Confirm</button><button className="button button-secondary" disabled={busy} onClick={cancel}>Cancel</button></div>
    </section>}
    {resetting && <section className="panel detail-body"><h2>Reset password for {recordName(resetting)}</h2><p>Previous sessions will be invalidated. The old password cannot be displayed.</p>
      <form onSubmit={event => { event.preventDefault(); if (password.length < 8 || password.length > 128) { setSaveError('Password must contain 8 to 128 characters.'); return } void performWrite(signal => resetManagerPassword(resetting.id, password, resetting.version, signal)) }}>
        <label className="form-field">New password<input type="password" minLength={8} maxLength={128} required autoComplete="new-password" disabled={busy} value={password} onChange={event => setPassword(event.target.value)} /></label>
        <div className="page-actions"><button className="button button-primary" disabled={busy}>Reset password</button><button className="button button-secondary" type="button" disabled={busy} onClick={cancel}>Cancel</button></div>
      </form>
    </section>}
    <section className="panel" aria-busy={loading}><div className="panel-heading"><h2>All {title.toLowerCase()}</h2></div>
      {loading ? <p className="loading-state" role="status">Loading…</p> : loadError ? <div className="detail-body" role="alert"><p>{loadError}</p><button className="button button-secondary" onClick={reload}>Try again</button></div> : records.length === 0 ? <p className="detail-body">No records yet. Add one to get started.</p> :
        <div className="table-scroll" role="region" aria-label={title} tabIndex={0}><table className="ticket-table"><thead><tr>{fields.map(field => <th key={field.key} scope="col">{field.label}</th>)}<th scope="col">Status</th><th scope="col">Actions</th></tr></thead>
          <tbody>{records.map(record => <tr key={record.id}>
            {fields.map(field => <td key={field.key}>{field.type === 'store' ? String(record.storeName ?? `Store #${record.storeId}`) : String(record[field.key])}</td>)}
            <td>{record.isActive ? 'Active' : 'Inactive'}</td><td><div className="workflow-buttons">
              <button className="button button-secondary" disabled={busy || formOpen} onClick={() => startEdit(record)}>Edit<span className="visually-hidden"> {recordName(record)}</span></button>
              <button className="button button-secondary" disabled={busy || formOpen} onClick={() => { cancel(); setActivation(record); setNotice('') }}>{record.isActive ? 'Deactivate' : 'Reactivate'}<span className="visually-hidden"> {recordName(record)}</span></button>
              {kind === 'store-managers' && <button className="button button-secondary" disabled={busy || formOpen} onClick={() => { cancel(); setResetting(record); setNotice('') }}>Reset password<span className="visually-hidden"> {recordName(record)}</span></button>}
            </div></td>
          </tr>)}</tbody>
        </table></div>}
    </section>
  </>
}
