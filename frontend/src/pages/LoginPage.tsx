import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { login } from '../api/auth'
import { setSession } from '../auth/session'

export function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending.current) return
    const controller = new AbortController()
    pending.current = controller
    setSaving(true)
    setError('')
    try {
      const session = await login(email, password, controller.signal)
      if (!controller.signal.aborted) setSession(session)
    } catch (failure: unknown) {
      if (!controller.signal.aborted) setError(failure instanceof Error && failure.name === 'Error'
        ? failure.message : 'Unable to sign in. Check your connection and try again.')
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setSaving(false)
    }
  }

  return (
    <main className="login-layout">
      <section className="panel login-panel" aria-labelledby="login-title">
        <p className="eyebrow">SERVICEDESK</p>
        <h1 id="login-title">Sign in to your workspace</h1>
        <p className="page-description">Manage maintenance requests for your stores.</p>
        <form onSubmit={submit} aria-busy={saving}>
          <fieldset className="ticket-fields" disabled={saving}>
            <legend className="visually-hidden">Sign-in details</legend>
            <div className="form-field field-full"><label htmlFor="login-email">Email</label>
              <input id="login-email" type="email" autoComplete="username" value={email} onChange={(event) => setEmail(event.target.value)} required maxLength={320} /></div>
            <div className="form-field field-full"><label htmlFor="login-password">Password</label>
              <input id="login-password" type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required /></div>
          </fieldset>
          {error && <p className="form-message error-message" role="alert">{error}</p>}
          <button className="button button-primary login-submit" type="submit" disabled={saving}>{saving ? 'Signing in…' : 'Sign in'}</button>
          {saving && <p className="visually-hidden" role="status">Signing in…</p>}
        </form>
        <p className="login-note">Your session stays in this tab's memory. Sign in again after refreshing the page.</p>
      </section>
    </main>
  )
}
