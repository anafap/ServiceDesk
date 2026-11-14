import { AppIcon } from './AppIcon'
import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { addComment, getComments } from '../api/comments'
import type { TicketComment } from '../api/comments'
import { formatUtcDateTime } from '../utils/dates'

export function TicketDiscussion({
  ticketId,
  readOnly,
  onAdded,
}: {
  ticketId: number
  readOnly: boolean
  onAdded: () => void
}) {
  const [items, setItems] = useState<TicketComment[]>([])
  const [cursor, setCursor] = useState<number | null>(null)
  const [visibleCount, setVisibleCount] = useState(5)
  const [text, setText] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [loaded, setLoaded] = useState(false)
  const pending = useRef<AbortController | null>(null)
  const read = useRef<AbortController | null>(null)
  useEffect(
    () => () => {
      pending.current?.abort()
      read.current?.abort()
    },
    [],
  )
  useEffect(() => {
    const controller = new AbortController()
    read.current?.abort()
    read.current = controller
    getComments(ticketId, null, controller.signal)
      .then((page) => {
        if (!controller.signal.aborted) {
          setItems(page.items)
          setVisibleCount(5)
          setCursor(page.nextBeforeId)
          setLoaded(true)
        }
      })
      .catch((e: unknown) => {
        if (!controller.signal.aborted)
          setError(
            e instanceof Error ? e.message : 'Discussion could not be loaded.',
          )
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [ticketId, reload])
  async function older() {
    if (loading) return
    if (visibleCount < items.length) {
      setVisibleCount(count => count + 5)
      return
    }
    if (!cursor) return
    const controller = new AbortController()
    read.current = controller
    setLoading(true)
    setError('')
    try {
      const page = await getComments(ticketId, cursor, controller.signal)
      if (!controller.signal.aborted) {
        setItems((current) => [...current, ...page.items])
        setVisibleCount(count => count + 5)
        setCursor(page.nextBeforeId)
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setError(
          e instanceof Error ? e.message : 'Discussion could not be loaded.',
        )
    } finally {
      if (!controller.signal.aborted) setLoading(false)
    }
  }
  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending.current || readOnly) return
    const controller = new AbortController()
    pending.current = controller
    setSaving(true)
    setError('')
    try {
      await addComment(ticketId, text, controller.signal)
      if (!controller.signal.aborted) {
        setText('')
        setLoading(true)
        setError('')
        setReload((v) => v + 1)
        onAdded()
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setError(
          e instanceof Error ? e.message : 'The comment could not be saved.',
        )
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setSaving(false)
    }
  }
  const chronologicalItems = [...items].sort((a, b) => a.id - b.id)

  return (
    <section className="panel" aria-labelledby="discussion-title">
      <div className="panel-heading">
        <h2 id="discussion-title">Discussion</h2>
        <button
          className="button button-icon button-ghost"
          aria-label="Refresh discussion"
          disabled={loading || saving}
          onClick={() => {
            setLoading(true)
            setError('')
            setReload((v) => v + 1)
          }}
        >
          <AppIcon name="refresh" />
        </button>
      </div>
      <div className="detail-body">
        {error && <p role="alert">{error}</p>}
        {loading && <p role="status">Loading discussion…</p>}
        {loaded && items.length === 0 && <p>No comments yet.</p>}
        {(cursor !== null || visibleCount < items.length) && (
          <button
            className="button button-secondary"
            disabled={loading}
            onClick={older}
          >
            Load older comments
          </button>
        )}
        <ol className="comment-list">
          {chronologicalItems.slice(-visibleCount).map((c) => (
            <li key={c.id}>
              <div className="comment-author">
                <span className="avatar">
                  {(c.authorName || 'U')
                    .split(' ')
                    .map((n) => n[0])
                    .slice(0, 2)
                    .join('')}
                </span>
                <strong>
                  {c.authorName || `User #${c.authorId}`}
                  <time>{formatUtcDateTime(c.createdAt)}</time>
                </strong>
              </div>
              <p className="full-description">{c.text}</p>
            </li>
          ))}
        </ol>
        {readOnly ? (
          <p>Discussion is read-only for closed or rejected tickets.</p>
        ) : (
          <form
            className="ticket-form-content discussion-form"
            onSubmit={submit}
          >
            <div className="form-field">
              <label htmlFor="comment-text">Add a comment</label>
              <textarea
                id="comment-text"
                rows={4}
                aria-describedby="comment-help"
                value={text}
                maxLength={2000}
                required
                disabled={saving}
                onChange={(e) => setText(e.target.value)}
              />
              <p className="field-hint" id="comment-help">
                Up to 2,000 characters. Comments cannot be edited or deleted.
              </p>
            </div>
            <div className="form-actions">
              <button
                className="button button-primary"
                disabled={saving || !text.trim()}
              >
                {saving ? 'Posting…' : 'Post comment'}
              </button>
            </div>
          </form>
        )}
      </div>
    </section>
  )
}
