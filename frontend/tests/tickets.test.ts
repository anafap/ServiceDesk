import assert from 'node:assert/strict'
import { afterEach, mock, test } from 'node:test'
import { getTickets } from '../src/api/tickets.ts'

const ticket = {
  id: 1, title: 'Printer issue', description: 'Paper is jammed',
  status: 1, priority: 3, storeId: 1, createdAt: '2026-10-21T09:00:00',
}

afterEach(() => mock.restoreAll())

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
  for (const data of [{ tickets: [] }, [{ ...ticket, status: 99 }], [{ ...ticket, priority: 0 }], [{ ...ticket, createdAt: 'invalid' }]]) {
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
