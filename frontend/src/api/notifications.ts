import { apiFetch } from './http.ts'
import { problemMessage } from './problems.ts'
export type Notification = { id: number; supportTicketId: number; kind: string; createdAt: string; readAt: string | null }
export type NotificationPage = { items: Notification[]; unreadCount: number; nextBeforeId: number | null }
export async function getNotifications(beforeId: number | null, signal: AbortSignal): Promise<NotificationPage> {
  const response = await apiFetch(`/api/notifications${beforeId ? `?beforeId=${beforeId}` : ''}`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error(await problemMessage(response, 'Notifications could not be loaded.'))
  const data: unknown = await response.json()
  if (!data || typeof data !== 'object') throw new Error('Unexpected notifications response.')
  const page = data as Record<string, unknown>
  if (!Number.isInteger(page.unreadCount) || Number(page.unreadCount) < 0
    || !(page.nextBeforeId === null || Number.isSafeInteger(page.nextBeforeId) && Number(page.nextBeforeId) > 0)
    || !Array.isArray(page.items) || page.items.length > 25 || !page.items.every((value: unknown) => {
      if (!value || typeof value !== 'object') return false
      const n = value as Record<string, unknown>
      return Number.isSafeInteger(n.id) && Number(n.id) > 0 && Number.isInteger(n.supportTicketId) && Number(n.supportTicketId) > 0
        && typeof n.kind === 'string' && typeof n.createdAt === 'string' && Number.isFinite(Date.parse(n.createdAt))
        && (n.readAt === null || typeof n.readAt === 'string' && Number.isFinite(Date.parse(n.readAt)))
    })) throw new Error('Unexpected notifications response.')
  return data as NotificationPage
}
export async function markNotificationRead(id: number, signal: AbortSignal) {
  const response = await apiFetch(`/api/notifications/${id}/read`, { method: 'POST', signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error(await problemMessage(response, 'Notification could not be marked as read.'))
}
