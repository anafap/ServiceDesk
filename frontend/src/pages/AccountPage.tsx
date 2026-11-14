import { useUnsavedChanges } from '../hooks/useUnsavedChanges'
import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { changePassword, getAccount, updateProfile } from '../api/account'
import type { AccountProfile } from '../api/account'
import { getSession, setSession } from '../auth/session'
import { PageHeader } from '../components/PageHeader'
import { AppIcon } from '../components/AppIcon'

export function AccountPage() {
  const [profile, setProfile] = useState<AccountProfile | null>(null)
  const [values, setValues] = useState({
    firstName: '',
    lastName: '',
    phoneNumber: '',
  })
  const [tab, setTab] = useState<'profile' | 'security'>('profile')
  const [securityDirty, setSecurityDirty] = useState(false)
  const [loading, setLoading] = useState(true)
  const [version, setVersion] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    getAccount(controller.signal)
      .then((p) => {
        if (!controller.signal.aborted) {
          setProfile(p)
          setValues({
            firstName: p.firstName,
            lastName: p.lastName,
            phoneNumber: p.phoneNumber,
          })
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setError(e instanceof Error ? e.message : 'Account unavailable.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [version])
  const dirty =
    !!profile &&
    (values.firstName !== profile.firstName ||
      values.lastName !== profile.lastName ||
      values.phoneNumber !== profile.phoneNumber)
  useUnsavedChanges(dirty || securityDirty)
  async function save(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!profile || pending.current) return
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const p = await updateProfile(
        { ...values, expectedVersion: profile.version },
        controller.signal,
      )
      if (!controller.signal.aborted) {
        setProfile(p)
        setValues({
          firstName: p.firstName,
          lastName: p.lastName,
          phoneNumber: p.phoneNumber,
        })
        const session = getSession()
        if (session)
          setSession({
            ...session,
            user: { ...session.user, name: `${p.firstName} ${p.lastName}` },
          })
        setNotice('Your profile has been updated.')
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setError(e instanceof Error ? e.message : 'Save failed.')
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  return (
    <>
      <PageHeader
        eyebrow="YOUR ACCOUNT"
        title="Account settings"
        description="Manage your personal details and keep your account secure."
      />
      <div className="settings-layout">
        <nav className="settings-nav" aria-label="Account settings">
          <button
            className={tab === 'profile' ? 'active' : ''}
            aria-current={tab === 'profile' ? 'page' : undefined}
            onClick={() => {
              if (
                !securityDirty ||
                window.confirm('Discard your unsaved password changes?')
              ) {
                setSecurityDirty(false)
                setTab('profile')
              }
            }}
          >
            <AppIcon name="user" />
            Profile
          </button>
          <button
            className={tab === 'security' ? 'active' : ''}
            aria-current={tab === 'security' ? 'page' : undefined}
            onClick={() => setTab('security')}
          >
            <AppIcon name="info" />
            Security
          </button>
        </nav>
        <div>
          {tab === 'security' ? (
            <PasswordSettings onDirtyChange={setSecurityDirty} />
          ) : (
            <section className="panel">
              <div className="panel-heading">
                <div>
                  <h2>Personal details</h2>
                  <p>Your name and contact information.</p>
                </div>
              </div>
              <div className="detail-body">
                {loading ? (
                  <div
                    className="skeleton-block"
                    role="status"
                    aria-label="Loading profile"
                  />
                ) : (
                  <>
                    {notice && (
                      <p className="form-message success-message" role="status">
                        {notice}
                      </p>
                    )}
                    {error && (
                      <div className="form-message error-message" role="alert">
                        <p>{error}</p>
                        <button
                          className="button button-secondary"
                          disabled={busy}
                          onClick={() => {
                            if (
                              !dirty ||
                              window.confirm(
                                'Discard your unsaved profile changes and reload?',
                              )
                            ) {
                              setLoading(true)
                              setError('')
                              setVersion((v) => v + 1)
                            }
                          }}
                        >
                          Reload profile
                        </button>
                      </div>
                    )}
                    {profile && (
                      <form onSubmit={save}>
                        <fieldset className="ticket-fields" disabled={busy}>
                          <legend className="visually-hidden">
                            Profile details
                          </legend>
                          <div className="form-field">
                            <label htmlFor="profile-first">First name</label>
                            <input
                              id="profile-first"
                              autoComplete="given-name"
                              required
                              maxLength={100}
                              value={values.firstName}
                              onChange={(e) =>
                                setValues({
                                  ...values,
                                  firstName: e.target.value,
                                })
                              }
                            />
                          </div>
                          <div className="form-field">
                            <label htmlFor="profile-last">Last name</label>
                            <input
                              id="profile-last"
                              autoComplete="family-name"
                              required
                              maxLength={100}
                              value={values.lastName}
                              onChange={(e) =>
                                setValues({
                                  ...values,
                                  lastName: e.target.value,
                                })
                              }
                            />
                          </div>
                          <div className="form-field field-full">
                            <label htmlFor="profile-phone">Phone number</label>
                            <input
                              id="profile-phone"
                              type="tel"
                              autoComplete="tel"
                              required
                              maxLength={50}
                              value={values.phoneNumber}
                              onChange={(e) =>
                                setValues({
                                  ...values,
                                  phoneNumber: e.target.value,
                                })
                              }
                            />
                          </div>
                        </fieldset>
                        <div className="account-access">
                          <h3>Workspace access</h3>
                          <dl className="ticket-metadata">
                            <div>
                              <dt>Email address</dt>
                              <dd>{profile.email}</dd>
                            </div>
                            <div>
                              <dt>Role</dt>
                              <dd>
                                {profile.role === 'Admin'
                                  ? 'Administrator'
                                  : 'Store manager'}
                              </dd>
                            </div>
                            <div>
                              <dt>Store access</dt>
                              <dd>
                                {profile.role === 'Admin'
                                  ? 'All stores'
                                  : profile.storeName ||
                                    `Store #${profile.storeId}`}
                              </dd>
                            </div>
                          </dl>
                          <p className="field-hint">
                            Contact your administrator to change your email or
                            workspace access.
                          </p>
                        </div>
                        <div className="form-actions">
                          <button
                            type="button"
                            className="button button-secondary"
                            disabled={!dirty || busy}
                            onClick={() =>
                              setValues({
                                firstName: profile.firstName,
                                lastName: profile.lastName,
                                phoneNumber: profile.phoneNumber,
                              })
                            }
                          >
                            Cancel changes
                          </button>
                          <button
                            className="button button-primary"
                            disabled={!dirty || busy}
                          >
                            {busy ? 'Saving…' : 'Save changes'}
                          </button>
                        </div>
                      </form>
                    )}
                  </>
                )}
              </div>
            </section>
          )}
        </div>
      </div>
    </>
  )
}
function PasswordSettings({
  onDirtyChange,
}: {
  onDirtyChange: (dirty: boolean) => void
}) {
  const [current, setCurrent] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [visible, setVisible] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])
  async function submit(e: SubmitEvent<HTMLFormElement>) {
    e.preventDefault()
    if (pending.current) return
    if (
      !password.trim() ||
      password.length < 8 ||
      password.length > 128 ||
      password !== confirm
    ) {
      setMessage('Enter matching new passwords of 8–128 characters.')
      return
    }
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    setMessage('')
    try {
      await changePassword(current, password, controller.signal)
      if (!controller.signal.aborted) {
        sessionStorage.setItem(
          'servicedesk-signin-notice',
          'Password updated. Sign in with your new password.',
        )
        setSession(null)
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setMessage(
          e instanceof Error ? e.message : 'Password could not be changed.',
        )
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>Change password</h2>
          <p>Choose a strong password you do not use elsewhere.</p>
        </div>
      </div>
      <form className="detail-body" onSubmit={submit} aria-busy={busy}>
        <p className="security-notice">
          <AppIcon name="info" />
          Changing your password signs out all your sessions. You will need to
          sign in again.
        </p>
        {message && (
          <p className="form-message error-message" role="alert">
            {message}
          </p>
        )}
        <fieldset className="ticket-fields" disabled={busy}>
          <legend className="visually-hidden">Change your password</legend>
          {[
            {
              id: 'current-password',
              label: 'Current password',
              value: current,
              set: setCurrent,
            },
            {
              id: 'new-password',
              label: 'New password',
              value: password,
              set: setPassword,
            },
            {
              id: 'confirm-password',
              label: 'Confirm new password',
              value: confirm,
              set: setConfirm,
            },
          ].map((f) => (
            <div className="form-field field-full" key={f.id}>
              <label htmlFor={f.id}>{f.label}</label>
              <input
                id={f.id}
                type={visible ? 'text' : 'password'}
                autoComplete={
                  f.id === 'current-password'
                    ? 'current-password'
                    : 'new-password'
                }
                required
                minLength={f.id === 'current-password' ? undefined : 8}
                maxLength={128}
                aria-invalid={
                  f.id === 'confirm-password' &&
                  !!confirm &&
                  confirm !== password
                }
                aria-describedby={
                  f.id === 'new-password'
                    ? 'password-length-hint'
                    : f.id === 'confirm-password' &&
                        confirm &&
                        confirm !== password
                      ? 'password-match-error'
                      : undefined
                }
                value={f.value}
                onChange={(e) => {
                  f.set(e.target.value)
                  onDirtyChange(
                    !!(
                      e.target.value ||
                      (f.id !== 'current-password' && current) ||
                      (f.id !== 'new-password' && password) ||
                      (f.id !== 'confirm-password' && confirm)
                    ),
                  )
                }}
              />
              {f.id === 'new-password' && (
                <p id="password-length-hint" className="field-hint">
                  Use 8–128 characters.
                </p>
              )}
              {f.id === 'confirm-password' &&
                confirm &&
                confirm !== password && (
                  <p id="password-match-error" className="field-error">
                    Passwords do not match.
                  </p>
                )}
            </div>
          ))}
        </fieldset>
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={visible}
            onChange={(e) => setVisible(e.target.checked)}
          />
          Show passwords
        </label>
        <div className="form-actions">
          <button className="button button-primary" disabled={busy}>
            {busy ? 'Changing…' : 'Change password'}
          </button>
        </div>
      </form>
    </section>
  )
}
