import { apiFetch } from './http.ts'
import { problemMessage } from './problems.ts'
export type Photo = { id: string; supportTicketId: number; authorId: number; state: 'pending' | 'processing' | 'ready' | 'rejected'; createdAt: string }
export async function getPhotos(ticketId: number, signal: AbortSignal): Promise<{ items: Photo[]; uploadsEnabled: boolean }> {
  const response = await apiFetch(`/api/tickets/${ticketId}/attachments`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error(await problemMessage(response, 'Photos could not be loaded.'))
  const data: unknown = await response.json()
  if (!data || typeof data !== 'object') throw new Error('Unexpected photo response.')
  const page = data as Record<string, unknown>
  if (typeof page.uploadsEnabled !== 'boolean' || !Array.isArray(page.items) || !page.items.every((value: unknown) => {
    if (!value || typeof value !== 'object') return false
    const p = value as Record<string, unknown>
    return typeof p.id === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(p.id)
      && p.supportTicketId === ticketId && Number.isInteger(p.authorId) && Number(p.authorId) > 0
      && ['pending', 'processing', 'ready', 'rejected'].includes(String(p.state))
      && typeof p.createdAt === 'string' && Number.isFinite(Date.parse(p.createdAt))
  })) throw new Error('Unexpected photo response.')
  return data as { items: Photo[]; uploadsEnabled: boolean }
}
export async function uploadPhoto(ticketId: number, file: File, signal: AbortSignal) {
  if (!['image/jpeg', 'image/png'].includes(file.type) || file.size > 5 * 1024 * 1024 || file.size === 0)
    throw new Error('Choose a JPEG or PNG photo no larger than 5 MiB.')
  const body = new FormData(); body.append('photo', file)
  let response: Response
  try { response = await apiFetch(`/api/tickets/${ticketId}/attachments`, { method: 'POST', body, signal: AbortSignal.any([signal, AbortSignal.timeout(30_000)]) }) }
  catch { throw new Error('We could not confirm the upload. Refresh photos before uploading again.') }
  if (response.status >= 500) throw new Error('We could not confirm the upload. Refresh photos before uploading again.')
  if (response.status !== 202) throw new Error(await problemMessage(response, 'Photo was not accepted.'))
}
export async function getPhotoContent(ticketId: number, id: string, signal: AbortSignal): Promise<Blob> {
  const response = await apiFetch(`/api/tickets/${ticketId}/attachments/${encodeURIComponent(id)}/content`, { signal: AbortSignal.any([signal, AbortSignal.timeout(30_000)]) })
  if (!response.ok || !response.headers.get('content-type')?.startsWith('image/png')) throw new Error('Photo is unavailable.')
  return response.blob()
}
