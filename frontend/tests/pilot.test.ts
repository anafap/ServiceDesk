import assert from 'node:assert/strict'
import { afterEach, beforeEach, mock, test } from 'node:test'
import { signInForTest } from './sessionFixture.ts'
import { setSession } from '../src/auth/session.ts'
import { getComments, addComment } from '../src/api/comments.ts'
import { getTickets, getDashboard } from '../src/api/tickets.ts'
import { changePassword } from '../src/api/account.ts'
import { getNotifications } from '../src/api/notifications.ts'
import { uploadPhoto, getPhotos } from '../src/api/photos.ts'
beforeEach(signInForTest)
afterEach(() => { mock.restoreAll(); setSession(null) })
test('comments preserve plain text and trust only server authors with valid paging', async () => {
  const c = { id: 26, supportTicketId: 1, authorId: 2, authorName: 'Manager', text: '<script>example</script>', createdAt: '2026-11-12T10:00:00Z' }
  const fetch = mock.method(globalThis, 'fetch', async () => Response.json({ items: [c], nextBeforeId: 26 }))
  assert.deepEqual((await getComments(1, null, new AbortController().signal)).items, [c])
  await getComments(1, 26, new AbortController().signal)
  assert.equal(fetch.mock.calls[1].arguments[0], '/api/tickets/1/comments?beforeId=26')
  mock.restoreAll()
  const post = mock.method(globalThis, 'fetch', async () => new Response(null, { status: 201 }))
  await addComment(1, ' <b>Plain text</b> ', new AbortController().signal)
  const options = post.mock.calls[0].arguments[1] as RequestInit
  assert.deepEqual(JSON.parse(String(options.body)), { text: '<b>Plain text</b>' })
  assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer test-access-token')
})
test('invalid comments never send; uncertain comments and password changes never retry', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => { throw new TypeError('connection lost') })
  await assert.rejects(addComment(1, ' ', new AbortController().signal), /Enter a comment/)
  assert.equal(fetch.mock.callCount(), 0)
  await assert.rejects(addComment(1, 'Comment', new AbortController().signal), /could not confirm/)
  assert.equal(fetch.mock.callCount(), 1)
  await assert.rejects(changePassword('current', 'new-password', new AbortController().signal), /could not confirm/)
  assert.equal(fetch.mock.callCount(), 2)
})
test('ticket filters and pagination are sent to the server', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => Response.json({ items: [], totalCount: 0, page: 2, pageSize: 25 }))
  await getTickets(new AbortController().signal, { page: 2, title: 'a & b', status: 1, priority: 'all' })
  assert.equal(fetch.mock.calls[0].arguments[0], '/api/tickets?page=2&title=a+%26+b&status=1')
})
test('dashboard uses a separate endpoint and validates counts', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => Response.json({ summary: { total: 31, open: 30, inProgress: 1, resolved: 0 }, recent: [], attention: [] }))
  assert.equal((await getDashboard(new AbortController().signal)).summary.total, 31)
  assert.equal(fetch.mock.calls[0].arguments[0], '/api/dashboard')
})
test('notification and photo contracts reject malformed or cross-ticket data', async () => {
  mock.method(globalThis, 'fetch', async () => Response.json({ items: [], unreadCount: -1, nextBeforeId: null }))
  await assert.rejects(getNotifications(null, new AbortController().signal), /Unexpected/)
  mock.restoreAll()
  mock.method(globalThis, 'fetch', async () => Response.json([{ id: 'bad', supportTicketId: 2, state: 'ready' }]))
  await assert.rejects(getPhotos(1, new AbortController().signal), /Unexpected/)
})
test('photo client rejects unsupported types before upload and does not retry an uncertain upload', async () => {
  const fetch = mock.method(globalThis, 'fetch', async () => { throw new TypeError('lost') })
  await assert.rejects(uploadPhoto(1, new File(['<svg/>'], 'photo.svg', { type: 'image/svg+xml' }), new AbortController().signal), /JPEG or PNG/)
  assert.equal(fetch.mock.callCount(), 0)
  await assert.rejects(uploadPhoto(1, new File(['fake image'], 'photo.png', { type: 'image/png' }), new AbortController().signal), /could not confirm/)
  assert.equal(fetch.mock.callCount(), 1)
})
