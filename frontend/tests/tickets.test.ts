import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { createTicket, getTickets } from '../src/api/tickets.ts'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'

const ticket = {
  id: 1, title: 'Printer issue', description: 'Paper is jammed',
  status: 1, priority: 3, storeId: 1, version: 'AAAAAAAAAAA=', createdAt: '2026-10-21T09:00:00',
}

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })

test('loads real-shaped ticket data through the relative API path', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json([ticket]))
  assert.deepEqual(await getTickets(new AbortController().signal), [ticket])
  assert.equal(fetchMock.mock.calls[0].arguments[0], '/api/tickets')
})

test('accepts an empty ticket list', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json([]))
  assert.deepEqual(await getTickets(new AbortController().signal), [])
})

test('reports unsuccessful HTTP responses instead of treating them as data', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('Unavailable', { status: 503 }))
  await assert.rejects(getTickets(new AbortController().signal), /HTTP 503/)
})

test('rejects malformed responses and unsupported status/priority values', async () => {
  for (const data of [{ tickets: [] }, [{ ...ticket, status: 99 }], [{ ...ticket, priority: 0 }], [{ ...ticket, version: 'AAAAAAAAAAA=', createdAt: 'invalid' }]]) {
    mock.method(globalThis, 'fetch', async () => Response.json(data))
    await assert.rejects(getTickets(new AbortController().signal), /unexpected ticket data/)
    mock.restoreAll()
  }
})

test('preserves network failures for the page to report', async () => {
  mock.method(globalThis, 'fetch', async () => { throw new TypeError('Failed to fetch') })
  await assert.rejects(getTickets(new AbortController().signal), TypeError)
})

test('passes cancellation to fetch so leaving the page can cancel a request', async () => {
  mock.method(globalThis, 'fetch', async (_input: unknown, options: RequestInit) => {
    options.signal?.throwIfAborted()
    return Response.json([])
  })
  const controller = new AbortController()
  controller.abort()
  await assert.rejects(getTickets(controller.signal), { name: 'AbortError' })
})

const createRequest = {
  title: 'Printer issue', description: 'Paper is jammed',
  categoryId: 2, storeId: 1, priority: 3 as const,
}

test('creates a ticket with numeric IDs and priority in a JSON POST', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json(ticket, { status: 201 }))
  assert.deepEqual(await createTicket(createRequest, new AbortController().signal), ticket)
  const [path, options] = fetchMock.mock.calls[0].arguments as unknown as [string, RequestInit]
  assert.equal(path, '/api/tickets')
  assert.equal(options.method, 'POST')
  assert.deepEqual(JSON.parse(String(options.body)), createRequest)
  assert.equal(new Headers(options.headers).get('Content-Type'), 'application/json')
  assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
  assert.equal(Object.hasOwn(JSON.parse(String(options.body)), 'createdByUserId'), false)
})

test('reports rejected create requests without claiming success', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('Invalid', { status: 400 }))
  await assert.rejects(createTicket(createRequest, new AbortController().signal), /not accepted \(HTTP 400\)/)
})

test('does not automatically retry an uncertain write after network failure', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => { throw new TypeError('Connection lost') })
  await assert.rejects(createTicket(createRequest, new AbortController().signal), /Refresh the ticket list before submitting again/)
  assert.equal(fetchMock.mock.callCount(), 1)
})

test('treats server failures or invalid success responses as unconfirmed saves', async () => {
  for (const response of [new Response('', { status: 500 }), Response.json({}), new Response('not JSON', { status: 201 })]) {
    mock.method(globalThis, 'fetch', async () => response)
    await assert.rejects(createTicket(createRequest, new AbortController().signal), /could not confirm/)
    mock.restoreAll()
  }
})
