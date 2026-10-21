import assert from 'node:assert/strict'
import { afterEach, mock, test } from 'node:test'
import { login } from '../src/api/auth.ts'
import { apiFetch } from '../src/api/http.ts'
import { getSession, setSession, subscribeSession } from '../src/auth/session.ts'
import { signInForTest } from './sessionFixture.ts'

afterEach(() => { mock.restoreAll(); setSession(null) })

test('login sends credentials only to the login endpoint and returns session metadata', async () => {
  signInForTest()
  const response = { ...getSession(), tokenType: 'Bearer' }
  setSession(null)
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json(response))
  const session = await login(' admin@example.test ', 'test-password', new AbortController().signal)
  assert.equal(session.user.role, 'Admin')
  const [path, options] = fetchMock.mock.calls[0].arguments as unknown as [string, RequestInit]
  assert.equal(path, '/api/auth/login')
  assert.deepEqual(JSON.parse(String(options.body)), { email: 'admin@example.test', password: 'test-password' })
  assert.equal(getSession(), null, 'the caller controls session activation')
})

test('invalid credentials have a generic error and never create a session', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 401 }))
  await assert.rejects(login('unknown@example.test', 'wrong', new AbortController().signal), /Email or password is incorrect/)
  assert.equal(getSession(), null)
})

test('malformed login responses are rejected', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json({ accessToken: 'bad', user: { passwordHash: 'must-not-be-used' } }))
  await assert.rejects(login('a@example.test', 'test', new AbortController().signal), /Unexpected sign-in response/)
})

test('protected requests send bearer authentication', async () => {
  signInForTest()
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json([]))
  await apiFetch('/api/tickets')
  const options = fetchMock.mock.calls[0].arguments[1] as RequestInit
  assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
})

test('missing or expired sessions do not make API calls', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json([]))
  await assert.rejects(apiFetch('/api/tickets'), /sign in/)
  signInForTest()
  setSession({ ...getSession()!, expiresAt: new Date(Date.now() - 1000).toISOString() })
  await assert.rejects(apiFetch('/api/tickets'), /sign in/)
  assert.equal(fetchMock.mock.callCount(), 0)
  assert.equal(getSession(), null)
})

test('401 clears the session while 403 preserves it', async () => {
  signInForTest()
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 403 }))
  assert.equal((await apiFetch('/api/external-technicians')).status, 403)
  assert.ok(getSession())
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 401 }))
  await assert.rejects(apiFetch('/api/tickets'), /session has expired/)
  assert.equal(getSession(), null)
})

test('logout notifies subscribers and removes the in-memory token', () => {
  signInForTest()
  let changes = 0
  const unsubscribe = subscribeSession(() => changes++)
  setSession(null)
  unsubscribe()
  assert.equal(getSession(), null)
  assert.equal(changes, 1)
})

test('an old request cannot sign out a newer session', async () => {
  signInForTest()
  mock.method(globalThis, 'fetch', async () => {
    setSession({ ...getSession()!, accessToken: 'new-access-token' })
    return new Response('', { status: 401 })
  })
  assert.equal((await apiFetch('/api/tickets')).status, 401)
  assert.equal(getSession()?.accessToken, 'new-access-token')
})
