import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { getReferenceData } from '../src/api/referenceData.ts'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'

beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })

test('loads stores and categories from their real endpoint paths', async () => {
  const fetchMock = mock.method(globalThis, 'fetch', async (path: string) => Response.json(
    path === '/api/stores' ? [{ id: 1, name: 'Main store', isActive: true }] : [{ id: 2, name: 'Equipment' }],
  ))
  const data = await getReferenceData(new AbortController().signal)
  assert.equal(data.stores[0].name, 'Main store')
  assert.equal(data.categories[0].name, 'Equipment')
  assert.deepEqual(fetchMock.mock.calls.map((call) => call.arguments[0]), ['/api/stores', '/api/categories'])
})

test('allows empty lookup data so the form can explain missing setup', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json([]))
  assert.deepEqual(await getReferenceData(new AbortController().signal), { stores: [], categories: [] })
})

test('reports lookup HTTP failures and malformed data', async () => {
  mock.method(globalThis, 'fetch', async () => new Response('', { status: 500 }))
  await assert.rejects(getReferenceData(new AbortController().signal), /could not be loaded/)
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async () => Response.json([{ id: '1', name: 'Wrong ID type' }]))
  await assert.rejects(getReferenceData(new AbortController().signal), /unexpected store or category data/)
})
