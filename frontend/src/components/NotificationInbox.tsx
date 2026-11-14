import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { getNotifications, markNotificationRead } from '../api/notifications'
import type { NotificationPage } from '../api/notifications'
import { formatUtcDateTime } from '../utils/dates'
import { AppIcon } from './AppIcon'
export function NotificationInbox() {
  const { pathname } = useLocation()
  const [page, setPage] = useState<NotificationPage>({
    items: [],
    unreadCount: 0,
    nextBeforeId: null,
  })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(true)
  const [marking, setMarking] = useState<number | null>(null)
  const pending = useRef<AbortController | null>(null)
  const element = useRef<HTMLDetailsElement>(null)
  async function load(beforeId: number | null = null) {
    pending.current?.abort()
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    try {
      const next = await getNotifications(beforeId, controller.signal)
      if (!controller.signal.aborted) {
        setPage((current) => ({
          ...next,
          items: beforeId
            ? [
                ...current.items,
                ...next.items.filter(
                  (n) => !current.items.some((old) => old.id === n.id),
                ),
              ]
            : next.items,
        }))
        setError('')
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setError(e instanceof Error ? e.message : 'Notifications unavailable.')
    } finally {
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  useEffect(() => {
    const initial = new AbortController()
    pending.current = initial
    getNotifications(null, initial.signal)
      .then((next) => {
        if (!initial.signal.aborted) {
          setPage(next)
          setError('')
        }
      })
      .catch((e) => {
        if (!initial.signal.aborted)
          setError(
            e instanceof Error ? e.message : 'Notifications unavailable.',
          )
      })
      .finally(() => {
        if (!initial.signal.aborted) setBusy(false)
      })
    element.current?.removeAttribute('open')
    const refresh = () => {
      if (document.visibilityState === 'visible') void load()
    }
    const outside = (event: MouseEvent) => {
      if (!element.current?.contains(event.target as Node))
        element.current?.removeAttribute('open')
    }
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && element.current?.open) {
        element.current.removeAttribute('open')
        element.current.querySelector('summary')?.focus()
      }
    }
    const timer = setInterval(refresh, 60_000)
    window.addEventListener('focus', refresh)
    document.addEventListener('visibilitychange', refresh)
    document.addEventListener('click', outside)
    document.addEventListener('keydown', escape)
    return () => {
      clearInterval(timer)
      pending.current?.abort()
      window.removeEventListener('focus', refresh)
      document.removeEventListener('visibilitychange', refresh)
      document.removeEventListener('click', outside)
      document.removeEventListener('keydown', escape)
    }
  }, [pathname])
  return (
    <details className="notification-inbox" ref={element}>
      <summary aria-label={`Notifications, ${page.unreadCount} unread`}>
        <AppIcon name="bell" />
        {page.unreadCount > 0 && (
          <span className="notification-count">
            {page.unreadCount > 99 ? '99+' : page.unreadCount}
          </span>
        )}
      </summary>
      <div className="notification-popover">
        <header>
          <h2>
            Notifications{' '}
            <span className="badge">{page.unreadCount} unread</span>
          </h2>
          <button
            className="button button-icon button-ghost"
            aria-label="Refresh notifications"
            disabled={busy}
            onClick={() => void load()}
          >
            <AppIcon name="refresh" />
          </button>
        </header>
        {error && (
          <p className="form-message error-message" role="alert">
            {error}
          </p>
        )}
        {busy && page.items.length === 0 && (
          <p className="notification-empty" role="status">
            Loading notifications…
          </p>
        )}
        {!busy && !error && page.items.length === 0 && (
          <p className="notification-empty">
            You're all caught up. Ticket updates will appear here.
          </p>
        )}
        <ul>
          {page.items.map((n) => (
            <li key={n.id} className={n.readAt === null ? 'unread' : ''}>
              <Link to={`/tickets/${n.supportTicketId}`}>
                {n.kind.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ')}{' '}
                · Ticket #{n.supportTicketId}
              </Link>
              <p>{formatUtcDateTime(n.createdAt)}</p>
              {n.readAt === null && (
                <button
                  className="button button-ghost"
                  disabled={marking !== null}
                  onClick={async () => {
                    setMarking(n.id)
                    setError('')
                    try {
                      await markNotificationRead(
                        n.id,
                        new AbortController().signal,
                      )
                      setPage((current) => ({
                        ...current,
                        unreadCount: Math.max(0, current.unreadCount - 1),
                        items: current.items.map((item) =>
                          item.id === n.id
                            ? { ...item, readAt: new Date().toISOString() }
                            : item,
                        ),
                      }))
                    } catch (e: unknown) {
                      setError(
                        e instanceof Error ? e.message : 'Update failed.',
                      )
                    } finally {
                      setMarking(null)
                    }
                  }}
                >
                  {marking === n.id ? 'Updating…' : 'Mark as read'}
                </button>
              )}
            </li>
          ))}
        </ul>
        {page.nextBeforeId && (
          <div className="detail-body">
            <button
              className="button button-secondary"
              disabled={busy}
              onClick={() => void load(page.nextBeforeId)}
            >
              {busy ? 'Loading…' : 'Load older notifications'}
            </button>
          </div>
        )}
      </div>
    </details>
  )
}
