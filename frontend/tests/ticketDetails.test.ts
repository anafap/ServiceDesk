import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { getTicketDetails, getTicketHistory, parseTicketId, TicketNotFoundError } from '../src/api/ticketDetails.ts'
import { setSession } from '../src/auth/session.ts'
import { signInForTest } from './sessionFixture.ts'

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })

const details = {
  id: 12, title: 'Printer issue', description: 'Full description\nSecond line', status: 2, priority: 3,
  storeId: 1, categoryId: 2, createdByUserId: 5, assetId: null, createdAt: '2026-10-21T09:00:00',
}
const history = {
  id: 1, supportTicketId: 12, previousStatus: 1, newStatus: 2, changedByUserId: 5,
  changedAt: '2026-10-21T09:30:00', reason: 'Approved for maintenance',
}

test('loads a specific ticket with bearer authentication and full metadata', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json(details))
  assert.deepEqual(await getTicketDetails(12, new AbortController().signal), details)
  const [path, options] = fetchMock.mock.calls[0].arguments as unknown as [string, RequestInit]
  assert.equal(path, '/api/tickets/12')
  assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
})

test('missing or inaccessible resources produce the same not-found error', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 404 }))
  await assert.rejects(getTicketDetails(12, new AbortController().signal), TicketNotFoundError)
  await assert.rejects(getTicketHistory(12, new AbortController().signal), TicketNotFoundError)
})

test('rejects details for a different ticket or malformed required metadata', async () => {
  for (const data of [{ ...details, id: 99 }, { ...details, categoryId: '2' }, { ...details, assetId: 0 }, { ...details, createdByUserId: null }]) {
    mock.method(globalThis, 'fetch', async () => Response.json(data))
    await assert.rejects(getTicketDetails(12, new AbortController().signal), /unexpected ticket details/)
    mock.restoreAll()
  }
})

test('loads actual history entries including an optional initial previous status', async () => {
  const entries = [{ ...history, previousStatus: null }, { ...history, id: 2 }]
  const fetchMock = mock.method(globalThis, 'fetch', async () => Response.json(entries))
  assert.deepEqual(await getTicketHistory(12, new AbortController().signal), entries)
  assert.equal(fetchMock.mock.calls[0].arguments[0], '/api/tickets/12/history')
})

test('empty history stays empty rather than inventing a creation event', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json([]))
  assert.deepEqual(await getTicketHistory(12, new AbortController().signal), [])
})

test('rejects history for another ticket or invalid statuses, actors, and timestamps', async () => {
  for (const data of [[{ ...history, supportTicketId: 99 }], [{ ...history, newStatus: 99 }], [{ ...history, previousStatus: 0 }], [{ ...history, changedByUserId: 0 }], [{ ...history, changedAt: 'invalid' }]]) {
    mock.method(globalThis, 'fetch', async () => Response.json(data))
    await assert.rejects(getTicketHistory(12, new AbortController().signal), /unexpected ticket history/)
    mock.restoreAll()
  }
})

test('reports HTTP failures and supports navigation cancellation', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 503 }))
  await assert.rejects(getTicketDetails(12, new AbortController().signal), /HTTP 503/)
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async (_path: unknown, options: RequestInit) => {
    options.signal?.throwIfAborted()
    return Response.json(details)
  })
  const controller = new AbortController()
  controller.abort()
  await assert.rejects(getTicketDetails(12, controller.signal), { name: 'AbortError' })
})

test('route IDs must fit the positive integer API route', () => {
  assert.equal(parseTicketId('12'), 12)
  for (const value of [undefined, '', '0', '-1', '1.2', 'abc', '1e2', '2147483648']) {
    assert.equal(parseTicketId(value), null)
  }
})
