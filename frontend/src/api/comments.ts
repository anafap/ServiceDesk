import { apiFetch } from './http.ts'
import { problemMessage } from './problems.ts'
export type TicketComment = { id: number; supportTicketId: number; authorId: number; authorName: string | null; text: string; createdAt: string }
export type CommentPage = { items: TicketComment[]; nextBeforeId: number | null }
export async function getComments(id: number, beforeId: number | null, signal: AbortSignal): Promise<CommentPage> {
  const response = await apiFetch(`/api/tickets/${id}/comments${beforeId ? `?beforeId=${beforeId}` : ''}`, {
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })
  if (!response.ok) throw new Error(await problemMessage(response, 'Discussion could not be loaded.'))
  const data: unknown = await response.json()
  if (!data || typeof data !== 'object') throw new Error('Unexpected discussion response.')
  const page = data as Record<string, unknown>
  if (!Array.isArray(page.items) || page.items.length > 25 || !page.items.every((c: unknown) => {
    if (!c || typeof c !== 'object') return false
    const item = c as Record<string, unknown>
    return Number.isInteger(item.id) && Number(item.id) > 0 && item.supportTicketId === id
      && Number.isInteger(item.authorId) && Number(item.authorId) > 0
      && (item.authorName === null || typeof item.authorName === 'string')
      && typeof item.text === 'string' && item.text.length <= 2000
      && typeof item.createdAt === 'string' && Number.isFinite(Date.parse(item.createdAt))
  }) || !(page.nextBeforeId === null || Number.isInteger(page.nextBeforeId) && Number(page.nextBeforeId) > 0))
    throw new Error('Unexpected discussion response.')
  return data as CommentPage
}
export async function addComment(id: number, text: string, signal: AbortSignal) {
  if (!text.trim() || text.trim().length > 2000) throw new Error('Enter a comment of 1–2,000 characters.')
  let response: Response
  try {
    response = await apiFetch(`/api/tickets/${id}/comments`, { method: 'POST',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text: text.trim() }),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch { throw new Error('We could not confirm the comment. Refresh discussion before trying again.') }
  if (response.status >= 500) throw new Error('We could not confirm the comment. Refresh discussion before trying again.')
  if (!response.ok) throw new Error(await problemMessage(response, 'The comment was not accepted.'))
}
