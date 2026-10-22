import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { editTicket } from '../src/api/editTicket.ts'
import { validateTicketDetails } from '../src/forms/ticketForm.ts'
import { setSession } from '../src/auth/session.ts'
import { signInForTest } from './sessionFixture.ts'

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })
const request = { title: 'Updated title', description: 'Updated description', priority: 3 as const, expectedVersion: 'AAAAAAAAAAA=' }

test('edits only mutable fields using PUT and bearer authentication', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }))
  await editTicket(12, request, new AbortController().signal)
  const [path, options] = fetchMock.mock.calls[0].arguments as unknown as [string, RequestInit]
  assert.equal(path, '/api/tickets/12')
  assert.equal(options.method, 'PUT')
  assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
  assert.deepEqual(JSON.parse(String(options.body)), request)
})

test('edit validation covers text limits, whitespace and priority without lookup fields', () => {
  assert.deepEqual(validateTicketDetails({ ...request, priority: '3' }), {})
  assert.ok(validateTicketDetails({ title: ' ', description: 'Description', priority: '3' }).title)
  assert.ok(validateTicketDetails({ title: 'x'.repeat(201), description: 'x'.repeat(4001), priority: '99' }).description)
  assert.ok(validateTicketDetails({ title: 'Title', description: 'Description', priority: '0' }).priority)
})

test('rejected edits report errors and never retry silently', async () => {
  for (const status of [400, 403, 404, 500]) {
    const fetchMock = mock.method(globalThis, 'fetch', async () => new Response('', { status }))
    await assert.rejects(editTicket(12, request, new AbortController().signal))
    assert.equal(fetchMock.mock.callCount(), 1)
    mock.restoreAll()
  }
})
