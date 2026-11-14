import { AppIcon } from './AppIcon'
import { useEffect, useRef, useState } from 'react'
import type { SubmitEvent } from 'react'
import { getPhotos, uploadPhoto, getPhotoContent } from '../api/photos'
import type { Photo } from '../api/photos'
import { formatUtcDateTime } from '../utils/dates'
function ReadyPhoto({ ticketId, id }: { ticketId: number; id: string }) {
  const [url, setUrl] = useState('')
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    let objectUrl = ''
    getPhotoContent(ticketId, id, controller.signal)
      .then((blob) => {
        if (!controller.signal.aborted) {
          objectUrl = URL.createObjectURL(blob)
          setUrl(objectUrl)
        }
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError('Photo could not be displayed. Refresh photos to try again.')
      })
    return () => {
      controller.abort()
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [ticketId, id])
  return error ? (
    <p role="alert">{error}</p>
  ) : url ? (
    <a href={url} target="_blank" rel="noreferrer">
      <img
        src={url}
        alt="Ticket repair photo; open full size"
        loading="lazy"
        className="ticket-photo"
      />
    </a>
  ) : (
    <p>Loading photo…</p>
  )
}
export function TicketPhotos({
  ticketId,
  readOnly,
  onUploaded,
}: {
  ticketId: number
  readOnly: boolean
  onUploaded: () => void
}) {
  const [photos, setPhotos] = useState<Photo[]>([])
  const [uploadsEnabled, setUploadsEnabled] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState('')
  useEffect(
    () => () => {
      if (preview) URL.revokeObjectURL(preview)
    },
    [preview],
  )
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reload, setReload] = useState(0)
  const pending = useRef<AbortController | null>(null)
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => () => pending.current?.abort(), [])
  useEffect(() => {
    const controller = new AbortController()
    getPhotos(ticketId, controller.signal)
      .then((page) => {
        if (!controller.signal.aborted) {
          setPhotos(page.items)
          setUploadsEnabled(page.uploadsEnabled)
          setError('')
        }
      })
      .catch((e: unknown) => {
        if (!controller.signal.aborted)
          setError(e instanceof Error ? e.message : 'Photos unavailable.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [ticketId, reload])
  async function submit(e: SubmitEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!file || pending.current || readOnly) return
    if (
      !['image/jpeg', 'image/png'].includes(file.type) ||
      file.size > 5 * 1024 * 1024
    ) {
      setError('Choose a JPEG or PNG photo up to 5 MB.')
      return
    }
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await uploadPhoto(ticketId, file, controller.signal)
      if (!controller.signal.aborted) {
        setFile(null)
        setPreview('')
        if (input.current) input.current.value = ''
        setNotice(
          'Photo uploaded for checking. Refresh photos to check its progress.',
        )
        setLoading(true)
        setReload((v) => v + 1)
        onUploaded()
      }
    } catch (e: unknown) {
      if (!controller.signal.aborted)
        setError(e instanceof Error ? e.message : 'Upload failed.')
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  return (
    <section className="panel" aria-labelledby="photos-title">
      <div className="panel-heading">
        <h2 id="photos-title">Photos</h2>
        <button
          className="button button-icon button-ghost"
          aria-label="Refresh photos"
          disabled={loading || busy}
          onClick={() => {
            setLoading(true)
            setReload((v) => v + 1)
          }}
        >
          <AppIcon name="refresh" />
        </button>
      </div>
      <div className="detail-body">
        {error && <p role="alert">{error}</p>}
        {notice && <p role="status">{notice}</p>}
        {loading && <p role="status">Loading photos…</p>}
        {!loading && photos.length === 0 && <p>No photos yet.</p>}
        <ul className="photo-list">
          {photos.map((p) => (
            <li key={p.id}>
              <p>
                {formatUtcDateTime(p.createdAt)} ·{' '}
                {p.state === 'ready'
                  ? 'Ready'
                  : p.state === 'rejected'
                    ? 'Photo could not be accepted'
                    : 'Checking photo…'}
              </p>
              {p.state === 'ready' && (
                <ReadyPhoto ticketId={ticketId} id={p.id} />
              )}
            </li>
          ))}
        </ul>
        {readOnly ? (
          <p>Photos are read-only for closed or rejected tickets.</p>
        ) : !uploadsEnabled ? (
          <p>
            Photo attachments are currently unavailable. You can describe the
            issue in Discussion.
          </p>
        ) : (
          <form
            onSubmit={submit}
            className="ticket-form-content discussion-form"
          >
            <div className="form-field">
              <label htmlFor="ticket-photo">Attach a photo</label>
              <input
                ref={input}
                id="ticket-photo"
                aria-describedby="photo-help"
                type="file"
                accept="image/jpeg,image/png"
                disabled={busy}
                onChange={(e) => {
                  const selected = e.target.files?.[0] ?? null
                  if (
                    selected &&
                    (!['image/jpeg', 'image/png'].includes(selected.type) ||
                      selected.size > 5 * 1024 * 1024)
                  ) {
                    setError('Choose a JPEG or PNG photo up to 5 MB.')
                    setFile(null)
                    setPreview('')
                    e.target.value = ''
                    return
                  }
                  setError('')
                  setFile(selected)
                  setPreview(selected ? URL.createObjectURL(selected) : '')
                }}
              />
              <p className="field-hint" id="photo-help">
                JPEG or PNG, up to 5 MiB and 20 megapixels. Up to five photos
                per ticket. Location metadata is removed before sharing.
              </p>
            </div>
            {preview && (
              <img
                className="photo-preview"
                src={preview}
                alt="Selected attachment preview"
              />
            )}
            <div className="form-actions">
              <button
                className="button button-primary"
                disabled={
                  busy ||
                  !file ||
                  photos.filter((p) => p.state !== 'rejected').length >= 5
                }
              >
                {busy ? 'Uploading…' : 'Upload photo'}
              </button>
            </div>
          </form>
        )}
      </div>
    </section>
  )
}
