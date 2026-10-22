import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'
import { login } from '../src/api/auth.ts'
import { editTicket } from '../src/api/editTicket.ts'
import { validVersion } from '../src/api/problems.ts'
import { listAdminRecords, resetManagerPassword, validateAdminInput } from '../src/api/administration.ts'

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })
test('record versions reject missing or malformed database versions', () => {
  assert.ok(validVersion('AAAAAAAAAAA='))
  for (const version of [undefined, null, '', 'wrong', 1]) assert.equal(validVersion(version), false)
})
test('stale edit reports conflict detail and never retries', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => Response.json({ detail: 'This record changed. Refresh before trying again.', traceId: 'test' }, { status: 409 }))
  await assert.rejects(editTicket(1, { title: 'Title', description: 'Details', priority: 1, expectedVersion: 'AAAAAAAAAAA=' }, new AbortController().signal), /record changed/)
  assert.equal(fetch.mock.callCount(), 1)
})
test('manager input validates email and assigned store', () => {
  const input = { firstName: 'Demo', lastName: 'Manager', email: 'manager@example.test', phoneNumber: '000', storeId: '1' }
  assert.deepEqual(validateAdminInput('store-managers', input), {})
  assert.ok(validateAdminInput('store-managers', { ...input, email: 'bad' }).email)
  assert.ok(validateAdminInput('store-managers', { ...input, storeId: '0' }).storeId)
})
test('manager list validates safe fields and password reset sends current version', async () => {
  const manager = { id: 1, firstName: 'Demo', lastName: 'Manager', email: 'manager@example.test', phoneNumber: '000', storeId: 1, storeName: 'Store', isActive: true, version: 'AAAAAAAAAAA=' }
  mock.method(globalThis, 'fetch', async () => Response.json([manager]))
  assert.deepEqual(await listAdminRecords('store-managers', new AbortController().signal), [manager])
  mock.restoreAll()
  const fetch = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 204 }))
  await resetManagerPassword(1, 'Test-only-password', manager.version, new AbortController().signal)
  const [path, options] = fetch.mock.calls[0].arguments as unknown as [string, RequestInit]
  assert.equal(path, '/api/admin/store-managers/1/reset-password')
  assert.deepEqual(JSON.parse(String(options.body)), { password: 'Test-only-password', expectedVersion: manager.version })
})
test('login rate limits show retry guidance without resubmitting', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => Response.json({ detail: 'Too many sign-in attempts.' }, { status: 429, headers: { 'Retry-After': '42' } }))
  await assert.rejects(login('test@example.test', 'Test-only-password', new AbortController().signal), /42 seconds/)
  assert.equal(fetch.mock.callCount(), 1)
})
