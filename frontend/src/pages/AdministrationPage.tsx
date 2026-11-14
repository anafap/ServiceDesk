import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { Link } from 'react-router'
import {
  listAdminRecords,
  resetManagerPassword,
  resourceFields,
  saveAdminRecord,
  validateAdminInput,
} from '../api/administration'
import type { AdminRecord, ResourceKind } from '../api/administration'
import { useSession } from '../auth/useSession'
import { PageHeader } from '../components/PageHeader'
import { FormPanel } from '../components/FormPanel'
import { ActionMenu } from '../components/ActionMenu'
import { AppIcon } from '../components/AppIcon'
import { EmptyState } from '../components/EmptyState'

const directory = {
  stores: {
    title: 'Stores',
    singular: 'store',
    description: 'Manage the locations that keep your business running.',
  },
  'store-managers': {
    title: 'Store managers',
    singular: 'store manager',
    description: 'Manage your team’s contact details and store access.',
  },
  'external-technicians': {
    title: 'Technicians',
    singular: 'technician',
    description: 'Your directory of external repair and maintenance contacts.',
  },
}
function recordName(record: AdminRecord) {
  return String(
    record.name ?? record.fullName ?? `${record.firstName} ${record.lastName}`,
  )
}
export function AdministrationPage({ kind }: { kind: ResourceKind }) {
  const session = useSession()
  if (session?.user.role !== 'Admin')
    return (
      <section className="panel detail-body">
        <h1>Administrator access required</h1>
        <p className="page-description">
          Your account does not have access to this area.
        </p>
        <Link className="button button-primary" to="/">
          Return to overview
        </Link>
      </section>
    )
  return <AdminWorkspace key={kind} kind={kind} />
}
function AdminWorkspace({ kind }: { kind: ResourceKind }) {
  const config = directory[kind]
  const fields = resourceFields[kind]
  const [records, setRecords] = useState<AdminRecord[]>([])
  const [stores, setStores] = useState<AdminRecord[]>([])
  const [storeError, setStoreError] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [version, setVersion] = useState(0)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [editing, setEditing] = useState<{
    id: number | null
    version: string
    values: Record<string, string>
  } | null>(null)
  const [initial, setInitial] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saveError, setSaveError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [activation, setActivation] = useState<AdminRecord | null>(null)
  const [resetting, setResetting] = useState<AdminRecord | null>(null)
  const [password, setPassword] = useState('')
  const [visible, setVisible] = useState(false)
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    listAdminRecords(kind, controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) setRecords(items)
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setLoadError(e instanceof Error ? e.message : 'Records unavailable.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    if (kind === 'store-managers')
      listAdminRecords('stores', controller.signal)
        .then((items) => {
          if (!controller.signal.aborted) {
            setStores(items)
            setStoreError('')
          }
        })
        .catch(() => {
          if (!controller.signal.aborted)
            setStoreError(
              'Store choices could not be loaded. Close this form and refresh to try again.',
            )
        })
    return () => controller.abort()
  }, [kind, version])
  function reload() {
    setLoading(true)
    setLoadError('')
    setVersion((v) => v + 1)
  }
  function cancel() {
    setEditing(null)
    setActivation(null)
    setResetting(null)
    setPassword('')
    setSaveError('')
    setErrors({})
    setVisible(false)
  }
  function startEdit(record: AdminRecord | null) {
    cancel()
    setNotice('')
    const values = Object.fromEntries(
      fields.map((field) => [
        field.key,
        record ? String(record[field.key]) : '',
      ]),
    )
    setInitial(JSON.stringify(values))
    setEditing({
      id: record?.id ?? null,
      version: record?.version ?? '',
      values,
    })
  }
  const dirty =
    !!editing && (initial !== JSON.stringify(editing.values) || !!password)
  function dismiss() {
    if (
      (!dirty && !password) ||
      window.confirm('Discard your unsaved changes?')
    )
      cancel()
  }
  async function performWrite(
    perform: (signal: AbortSignal) => Promise<void>,
    message: string,
  ) {
    if (pending.current) return
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    setSaveError('')
    setNotice('')
    try {
      await perform(controller.signal)
      if (!controller.signal.aborted) {
        cancel()
        setNotice(message)
        reload()
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setSaveError(
          e instanceof Error
            ? e.message
            : 'Unable to save. Your changes have been kept.',
        )
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!editing) return
    const validation = validateAdminInput(kind, editing.values)
    if (
      kind === 'store-managers' &&
      editing.id === null &&
      (!password.trim() || password.length < 8 || password.length > 128)
    )
      validation.password = 'Use 8–128 characters.'
    setErrors(validation)
    if (Object.keys(validation).length) {
      const field = event.currentTarget.elements.namedItem(
        Object.keys(validation)[0],
      )
      if (field instanceof HTMLElement) field.focus()
      return
    }
    const input: Record<string, string | number | boolean> = Object.fromEntries(
      Object.entries(editing.values).map(([key, value]) => [key, value.trim()]),
    )
    if (kind === 'store-managers') {
      input.storeId = Number(editing.values.storeId)
      if (editing.id === null) input.password = password
    }
    if (editing.id !== null) input.expectedVersion = editing.version
    void performWrite(
      (signal) => saveAdminRecord(kind, editing.id, input, signal),
      `${config.singular[0].toUpperCase() + config.singular.slice(1)} ${editing.id === null ? 'added' : 'updated'} successfully.`,
    )
  }
  const filtered = records.filter(
    (record) =>
      (status === 'all' || record.isActive === (status === 'active')) &&
      Object.values(record).some((value) =>
        String(value ?? '')
          .toLowerCase()
          .includes(search.toLowerCase()),
      ),
  )
  function rowActions(record: AdminRecord) {
    return (
      <div className="action-cell">
        <button
          className="button button-secondary"
          onClick={() => startEdit(record)}
          aria-label={`Edit ${recordName(record)}`}
        >
          Edit
        </button>
        <ActionMenu label={`More actions for ${recordName(record)}`}>
          <button
            className={record.isActive ? 'danger-text' : ''}
            onClick={() => {
              cancel()
              setActivation(record)
              setNotice('')
            }}
          >
            {record.isActive ? 'Deactivate' : 'Reactivate'}
          </button>
          {kind === 'store-managers' && (
            <button
              onClick={() => {
                cancel()
                setResetting(record)
                setNotice('')
              }}
            >
              Reset password
            </button>
          )}
        </ActionMenu>
      </div>
    )
  }
  function statusBadge(record: AdminRecord) {
    return (
      <span
        className={`status-tag ${record.isActive ? 'status-5' : 'status-6'}`}
      >
        {record.isActive ? 'Active' : 'Inactive'}
      </span>
    )
  }
  return (
    <>
      <PageHeader
        eyebrow="ADMINISTRATION"
        title={config.title}
        description={config.description}
        actions={
          <>
            <button
              className="button button-icon button-secondary"
              aria-label={`Refresh ${config.title.toLowerCase()}`}
              disabled={loading || busy}
              onClick={() => reload()}
            >
              <AppIcon name="refresh" />
            </button>
            <button
              className="button button-primary"
              onClick={() => startEdit(null)}
            >
              <AppIcon name="plus" />
              Add {config.singular}
            </button>
          </>
        }
      />
      {notice && (
        <p className="form-message success-message" role="status">
          {notice}
        </p>
      )}
      {(editing || activation || resetting) && (
        <FormPanel
          title={
            editing
              ? `${editing.id === null ? 'Add' : 'Edit'} ${config.singular}`
              : activation
                ? `${activation.isActive ? 'Deactivate' : 'Reactivate'} ${config.singular}`
                : 'Reset manager password'
          }
          onClose={cancel}
          dirty={dirty || !!password}
          busy={busy}
        >
          {saveError && (
            <p className="form-message error-message" role="alert">
              {saveError}
            </p>
          )}
          {editing && (
            <form onSubmit={submit} noValidate aria-busy={busy}>
              {storeError && (
                <p className="form-message error-message" role="alert">
                  {storeError}
                </p>
              )}
              <fieldset className="ticket-fields" disabled={busy}>
                <legend className="visually-hidden">
                  {config.singular} details
                </legend>
                {fields.map((field) => (
                  <div
                    className={`form-field ${field.key === 'email' || field.key === 'address' || field.key === 'storeId' ? 'field-full' : ''}`}
                    key={field.key}
                  >
                    <label htmlFor={`admin-${field.key}`}>
                      {field.label} <span>Required</span>
                    </label>
                    {field.type === 'store' ? (
                      <select
                        id={`admin-${field.key}`}
                        name={field.key}
                        required
                        aria-invalid={!!errors[field.key]}
                        aria-describedby={
                          errors[field.key] ? `error-${field.key}` : undefined
                        }
                        value={editing.values[field.key]}
                        onChange={(e) => {
                          setEditing({
                            ...editing,
                            values: {
                              ...editing.values,
                              [field.key]: e.target.value,
                            },
                          })
                          setErrors({ ...errors, [field.key]: '' })
                        }}
                      >
                        <option value="">Select an active store</option>
                        {stores
                          .filter(
                            (store) =>
                              store.isActive ||
                              String(store.id) === editing.values.storeId,
                          )
                          .map((store) => (
                            <option key={store.id} value={store.id}>
                              {String(store.name)}
                              {store.isActive
                                ? ''
                                : ' (inactive; existing assignment)'}
                            </option>
                          ))}
                      </select>
                    ) : (
                      <input
                        id={`admin-${field.key}`}
                        name={field.key}
                        required
                        type={
                          field.key === 'phoneNumber'
                            ? 'tel'
                            : (field.type ?? 'text')
                        }
                        maxLength={field.maxLength}
                        value={editing.values[field.key]}
                        aria-invalid={!!errors[field.key]}
                        aria-describedby={
                          errors[field.key] ? `error-${field.key}` : undefined
                        }
                        onChange={(e) => {
                          setEditing({
                            ...editing,
                            values: {
                              ...editing.values,
                              [field.key]: e.target.value,
                            },
                          })
                          setErrors({ ...errors, [field.key]: '' })
                        }}
                      />
                    )}
                    {errors[field.key] && (
                      <p className="field-error" id={`error-${field.key}`}>
                        {errors[field.key]}
                      </p>
                    )}
                  </div>
                ))}
                {kind === 'store-managers' && editing.id === null && (
                  <div className="form-field field-full">
                    <label htmlFor="initial-password">Initial password</label>
                    <input
                      id="initial-password"
                      name="password"
                      type={visible ? 'text' : 'password'}
                      autoComplete="new-password"
                      required
                      minLength={8}
                      maxLength={128}
                      value={password}
                      aria-invalid={!!errors.password}
                      aria-describedby="initial-password-help"
                      onChange={(e) => setPassword(e.target.value)}
                    />
                    <p
                      id="initial-password-help"
                      className={errors.password ? 'field-error' : 'field-hint'}
                    >
                      {errors.password ||
                        'Use 8–128 characters. Share this password with the manager securely.'}
                    </p>
                  </div>
                )}
              </fieldset>
              {kind === 'store-managers' && editing.id === null && (
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={visible}
                    onChange={(e) => setVisible(e.target.checked)}
                  />
                  Show password
                </label>
              )}
              <div className="form-actions">
                <button
                  className="button button-secondary"
                  type="button"
                  disabled={busy}
                  onClick={dismiss}
                >
                  Cancel
                </button>
                <button
                  className="button button-primary"
                  disabled={
                    busy ||
                    (kind === 'store-managers' &&
                      (!!storeError || !stores.length))
                  }
                >
                  {busy
                    ? 'Saving…'
                    : editing.id === null
                      ? `Add ${config.singular}`
                      : 'Save changes'}
                </button>
              </div>
            </form>
          )}
          {activation && (
            <>
              <h2>
                {activation.isActive ? 'Deactivate' : 'Reactivate'}{' '}
                {recordName(activation)}?
              </h2>
              <p>
                {kind === 'store-managers'
                  ? 'This changes the manager’s ability to sign in and invalidates their current sessions. Existing tickets and history will remain available.'
                  : activation.isActive
                    ? 'This record will no longer be offered for new work. Existing tickets and history remain available.'
                    : 'This record will be available for new work again.'}
              </p>
              <div className="form-actions">
                <button
                  className="button button-secondary"
                  disabled={busy}
                  onClick={cancel}
                >
                  Cancel
                </button>
                <button
                  className={`button ${activation.isActive ? 'button-danger' : 'button-primary'}`}
                  disabled={busy}
                  onClick={() =>
                    void performWrite(
                      (signal) =>
                        saveAdminRecord(
                          kind,
                          activation.id,
                          {
                            isActive: !activation.isActive,
                            expectedVersion: activation.version,
                          },
                          signal,
                          true,
                        ),
                      `${recordName(activation)} ${activation.isActive ? 'deactivated' : 'reactivated'}.`,
                    )
                  }
                >
                  {busy
                    ? 'Updating…'
                    : activation.isActive
                      ? 'Deactivate'
                      : 'Reactivate'}
                </button>
              </div>
            </>
          )}
          {resetting && (
            <>
              <h2>{recordName(resetting)}</h2>
              <p>
                Set a new password for this manager. All their current sessions
                will be signed out.
              </p>
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  void performWrite(
                    (signal) =>
                      resetManagerPassword(
                        resetting.id,
                        password,
                        resetting.version,
                        signal,
                      ),
                    'Password reset. The manager must sign in with the new password.',
                  )
                }}
              >
                <div className="form-field">
                  <label htmlFor="reset-password">New password</label>
                  <input
                    id="reset-password"
                    type={visible ? 'text' : 'password'}
                    required
                    minLength={8}
                    maxLength={128}
                    autoComplete="new-password"
                    disabled={busy}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                  <p className="field-hint">
                    Use 8–128 characters. Share the new password securely.
                  </p>
                </div>
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={visible}
                    onChange={(e) => setVisible(e.target.checked)}
                  />
                  Show password
                </label>
                <div className="form-actions">
                  <button
                    type="button"
                    className="button button-secondary"
                    disabled={busy}
                    onClick={dismiss}
                  >
                    Cancel
                  </button>
                  <button className="button button-primary" disabled={busy}>
                    {busy ? 'Resetting…' : 'Reset password'}
                  </button>
                </div>
              </form>
            </>
          )}
        </FormPanel>
      )}
      <section className="panel" aria-busy={loading}>
        <div className="panel-heading">
          <h2>
            {kind === 'external-technicians'
              ? 'External technician directory'
              : `All ${config.title.toLowerCase()}`}
          </h2>
          <span className="badge">
            {records.length}{' '}
            {records.length === 1
              ? config.singular
              : config.title.toLowerCase()}
          </span>
        </div>
        <div
          className="ticket-filters"
          role="search"
          aria-label={`Filter ${config.title.toLowerCase()}`}
        >
          <label>
            Search
            <input
              type="search"
              placeholder={`Search ${config.title.toLowerCase()}…`}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label>
            Status
            <select value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="all">All records</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
          {(search || status !== 'all') && (
            <button
              className="button button-ghost"
              onClick={() => {
                setSearch('')
                setStatus('all')
              }}
            >
              Clear filters
            </button>
          )}
        </div>
        {loading ? (
          <div
            role="status"
            aria-label={`Loading ${config.title.toLowerCase()}`}
          >
            <div className="skeleton-row" />
            <div className="skeleton-row" />
          </div>
        ) : loadError ? (
          <div role="alert">
            <EmptyState
              icon="info"
              title="Directory could not be loaded"
              description={loadError}
            >
              <button
                className="button button-secondary"
                onClick={() => reload()}
              >
                Try again
              </button>
            </EmptyState>
          </div>
        ) : !filtered.length ? (
          <EmptyState
            icon="overview"
            title={
              records.length
                ? 'No matching records'
                : `No ${config.title.toLowerCase()} yet`
            }
            description={
              records.length
                ? 'Try another search or status.'
                : `Add your first ${config.singular} to get started.`
            }
          />
        ) : (
          <>
            <div
              className="table-scroll admin-table-wrap"
              role="region"
              aria-label={config.title}
              tabIndex={0}
            >
              <table className="ticket-table admin-table">
                <thead>
                  <tr>
                    <th scope="col">
                      {kind === 'stores'
                        ? 'Store'
                        : kind === 'store-managers'
                          ? 'Manager'
                          : 'Technician'}
                    </th>
                    <th scope="col">
                      {kind === 'stores'
                        ? 'Location'
                        : kind === 'store-managers'
                          ? 'Contact'
                          : 'Company / specialisation'}
                    </th>
                    <th scope="col">
                      {kind === 'store-managers'
                        ? 'Assigned store'
                        : kind === 'stores'
                          ? 'Store code'
                          : 'Phone'}
                    </th>
                    <th scope="col">Status</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((record) => (
                    <tr key={record.id}>
                      <td>
                        <strong className="record-name">
                          {recordName(record)}
                        </strong>
                        {kind === 'external-technicians' && (
                          <span className="record-secondary">
                            External contact
                          </span>
                        )}
                      </td>
                      <td>
                        {kind === 'stores' ? (
                          <>
                            {String(record.address)}
                            <span className="record-secondary">
                              {String(record.city)}
                            </span>
                          </>
                        ) : kind === 'store-managers' ? (
                          <>
                            <a href={`mailto:${record.email}`}>
                              {String(record.email)}
                            </a>
                            <a
                              className="record-secondary"
                              href={`tel:${record.phoneNumber}`}
                            >
                              {String(record.phoneNumber)}
                            </a>
                          </>
                        ) : (
                          <>
                            {String(record.companyName)}
                            <span className="record-secondary">
                              {String(record.specialisation)}
                            </span>
                          </>
                        )}
                      </td>
                      <td>
                        {kind === 'stores' ? (
                          <span className="badge">
                            {String(record.storeCode)}
                          </span>
                        ) : kind === 'store-managers' ? (
                          String(record.storeName ?? `Store #${record.storeId}`)
                        ) : (
                          <a href={`tel:${record.phoneNumber}`}>
                            {String(record.phoneNumber)}
                          </a>
                        )}
                      </td>
                      <td>{statusBadge(record)}</td>
                      <td>{rowActions(record)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="mobile-directory-list">
              {filtered.map((record) => (
                <li key={record.id}>
                  <div className="mobile-directory-heading">
                    <strong className="record-name">
                      {recordName(record)}
                    </strong>
                    {statusBadge(record)}
                  </div>
                  <p className="record-secondary">
                    {kind === 'stores'
                      ? `${record.address}, ${record.city}`
                      : kind === 'store-managers'
                        ? `${record.email} · ${record.storeName ?? `Store #${record.storeId}`}`
                        : `${record.companyName} · ${record.specialisation}`}
                  </p>
                  {kind !== 'stores' && (
                    <a
                      className="record-secondary"
                      href={`tel:${record.phoneNumber}`}
                    >
                      {String(record.phoneNumber)}
                    </a>
                  )}
                  <div className="mobile-directory-actions">
                    {rowActions(record)}
                  </div>
                </li>
              ))}
            </ul>
            <div className="directory-count">
              Showing {filtered.length} of {records.length}{' '}
              {config.title.toLowerCase()}
            </div>
          </>
        )}
      </section>
    </>
  )
}
