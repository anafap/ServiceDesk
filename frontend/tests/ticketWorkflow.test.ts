import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { getAssignment, getTechnicians, performTicketAction } from '../src/api/ticketWorkflow.ts'
import { availableActions, validateAssignment } from '../src/forms/ticketWorkflow.ts'
import type { TicketStatus } from '../src/api/tickets.ts'
import { getSession, setSession } from '../src/auth/session.ts'
import { signInForTest } from './sessionFixture.ts'

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })

test('admin actions follow the existing lifecycle and terminal states expose no actions', () => {
  const user = getSession()!.user
  const expected = [['approve', 'reject'], ['assign'], ['progress'], ['resolve'], ['close'], [], []]
  expected.forEach((actions, index) => assert.deepEqual(availableActions({ storeId: 1, status: (index + 1) as TicketStatus }, user), actions))
})

test('manager can progress, resolve and close only own-store tickets', () => {
  const manager = { id: 21, name: 'Manager', role: 'StoreManager', storeId: 1 } as const
  for (const [status, expected] of [[1, []], [2, []], [3, ['progress']], [4, ['resolve']], [5, ['close']], [6, []], [7, []]] as const) {
    assert.deepEqual(availableActions({ storeId: 1, status }, manager), expected)
    assert.deepEqual(availableActions({ storeId: 2, status }, manager), [])
  }
})

test('assignment requires a listed technician and future local visit time', () => {
  const technicians = [{ id: 1, fullName: 'Demo', companyName: 'Demo company' }]
  const now = Date.parse('2026-01-01T00:00:00Z')
  assert.deepEqual(validateAssignment('1', '2099-01-01T12:00', technicians, now), {})
  assert.ok(validateAssignment('99', '2099-01-01T12:00', technicians, now).technicianId)
  for (const value of ['', 'invalid', '2020-01-01T12:00']) assert.ok(validateAssignment('1', value, technicians, now).scheduledAt)
})

test('loads actual technician options and preserves the empty-data state', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json([{ id: 1, fullName: 'Demo', companyName: 'Demo company' }]))
  assert.equal((await getTechnicians(new AbortController().signal))[0].fullName, 'Demo')
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async () => Response.json([]))
  assert.deepEqual(await getTechnicians(new AbortController().signal), [])
})

test('workflow actions POST to real endpoint paths with bearer auth and no actor IDs', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }))
  for (const action of ['approve', 'reject', 'progress', 'resolve', 'close'] as const) {
    await performTicketAction(12, action, { reason: 'Confirmed' }, new AbortController().signal)
    const [path, options] = fetchMock.mock.calls.at(-1)!.arguments as unknown as [string, RequestInit]
    assert.equal(path, `/api/tickets/12/${action}`)
    assert.equal(options.method, 'POST')
    assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
    assert.deepEqual(JSON.parse(String(options.body)), { reason: 'Confirmed' })
  }
})

test('assignment posts the selected technician, UTC timestamp and notes', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }))
  const request = { externalTechnicianId: 1, scheduledAt: '2099-01-01T09:00:00.000Z', notes: 'Bring parts', reason: null }
  await performTicketAction(12, 'assign', request, new AbortController().signal)
  const options = fetchMock.mock.calls[0].arguments[1] as RequestInit
  assert.deepEqual(JSON.parse(String(options.body)), request)
})

test('saved assignment handles no-record and populated responses', async () => {
  mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }))
  assert.equal(await getAssignment(12, new AbortController().signal), null)
  mock.restoreAll()
  const assignment = { id: 1, supportTicketId: 12, externalTechnicianId: 1, assignedByUserId: 1, assignedAt: '2026-01-01T12:00:00Z', scheduledAt: '2099-01-01T09:00:00Z', notes: 'Visit' }
  mock.method(globalThis, 'fetch', async () => Response.json(assignment))
  assert.deepEqual(await getAssignment(12, new AbortController().signal), assignment)
})

test('workflow conflicts and uncertain writes report errors without automatic retries', async () => {
  for (const status of [400, 403, 404, 409, 500]) {
    const fetchMock = mock.method(globalThis, 'fetch', async () => new Response('', { status }))
    await assert.rejects(performTicketAction(12, 'approve', { reason: null }, new AbortController().signal))
    assert.equal(fetchMock.mock.callCount(), 1)
    mock.restoreAll()
  }
  const fetchMock = mock.method(globalThis, 'fetch', async () => { throw new TypeError('Connection lost') })
  await assert.rejects(performTicketAction(12, 'approve', { reason: null }, new AbortController().signal), /Refresh ticket details/)
  assert.equal(fetchMock.mock.callCount(), 1)
})
