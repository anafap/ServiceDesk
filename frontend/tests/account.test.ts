import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'
import { getAccount, updateProfile } from '../src/api/account.ts'
const profile = {
  id: 1,
  firstName: 'Test',
  lastName: 'Admin',
  email: 'test@example.test',
  phoneNumber: '123',
  role: 'Admin',
  storeId: null,
  storeName: null,
  version: 'AAAAAAAAAAA=',
}
beforeEach(signInForTest)
afterEach(() => {
  mock.restoreAll()
  setSession(null)
})
test('profile read and update use authenticated current-account endpoints and concurrency', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () =>
    Response.json(profile),
  )
  assert.deepEqual(await getAccount(new AbortController().signal), profile)
  await updateProfile(
    {
      firstName: 'Updated',
      lastName: 'Admin',
      phoneNumber: '456',
      expectedVersion: profile.version,
    },
    new AbortController().signal,
  )
  assert.equal(fetch.mock.calls[0].arguments[0], '/api/account')
  assert.equal(fetch.mock.calls[1].arguments[0], '/api/account/profile')
  const options = fetch.mock.calls[1].arguments[1] as RequestInit
  assert.equal(options.method, 'PUT')
  assert.equal(
    new Headers(options.headers).get('Authorization'),
    'Bearer test-access-token',
  )
  assert.deepEqual(JSON.parse(String(options.body)), {
    firstName: 'Updated',
    lastName: 'Admin',
    phoneNumber: '456',
    expectedVersion: profile.version,
  })
})
test('profile rejects malformed responses and reports conflicts without retry', async () => {
  mock.method(globalThis, 'fetch', async () =>
    Response.json({ ...profile, version: 'bad' }),
  )
  await assert.rejects(getAccount(new AbortController().signal), /Unexpected/)
  mock.restoreAll()
  const fetch = mock.method(globalThis, 'fetch', async () =>
    Response.json({ detail: 'This record changed.' }, { status: 409 }),
  )
  await assert.rejects(
    updateProfile(
      {
        firstName: 'New',
        lastName: 'Name',
        phoneNumber: '123',
        expectedVersion: profile.version,
      },
      new AbortController().signal,
    ),
    /changed/,
  )
  assert.equal(fetch.mock.callCount(), 1)
})
