import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { listAdminRecords, saveAdminRecord, validateAdminInput } from '../src/api/administration.ts'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'
beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })
test('administration validates required values and database length limits', () => {
  assert.deepEqual(validateAdminInput('stores', { storeCode: ' S1 ', name: 'Store', address: 'Address', city: 'City' }), {})
  assert.ok(validateAdminInput('stores', { storeCode: ' ', name: 'N'.repeat(51), address: 'A'.repeat(51), city: 'City' }).storeCode)
  assert.ok(validateAdminInput('external-technicians', { fullName: 'Name', companyName: 'Company', phoneNumber: '1'.repeat(21), specialisation: 'HVAC' }).phoneNumber)
})
test('admin API validates data and includes inactive records', async () => {
  const records = [{ id: 1, storeCode: 'S1', name: 'Store', address: 'Address', city: 'City', isActive: false, version: 'AAAAAAAAAAA=' }]
  mock.method(globalThis, 'fetch', async () => Response.json(records))
  assert.deepEqual(await listAdminRecords('stores', new AbortController().signal), records)
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async () => Response.json([{ id: 1 }]))
  await assert.rejects(listAdminRecords('stores', new AbortController().signal), /Unexpected/)
})
test('administration uses authenticated create edit and activation endpoints', async () => {
  const calls: { path: string; method: string; body: unknown }[] = []
  mock.method(globalThis, 'fetch', async (path: string, options: RequestInit) => {
    assert.match(new Headers(options.headers).get('Authorization')!, /^Bearer /)
    calls.push({ path, method: options.method!, body: JSON.parse(String(options.body)) })
    return new Response(null, { status: 204 })
  })
  const signal = new AbortController().signal
  await saveAdminRecord('stores', null, { name: 'New' }, signal)
  await saveAdminRecord('external-technicians', 2, { fullName: 'Edited' }, signal)
  await saveAdminRecord('stores', 1, { isActive: false, version: 'AAAAAAAAAAA=' }, signal, true)
  assert.deepEqual(calls.map(call => [call.path, call.method]), [['/api/admin/stores', 'POST'], ['/api/admin/external-technicians/2', 'PUT'], ['/api/admin/stores/1/status', 'PATCH']])
})
test('conflicts and uncertain writes are reported without retries', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 409 }))
  await assert.rejects(saveAdminRecord('stores', null, {}, new AbortController().signal), /already in use/)
  assert.equal(fetch.mock.callCount(), 1)
})
