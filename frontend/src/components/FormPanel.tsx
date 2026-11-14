import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { AppIcon } from './AppIcon'

export function FormPanel({
  title,
  children,
  onClose,
  dirty = false,
  busy = false,
}: {
  title: string
  children: ReactNode
  onClose: () => void
  dirty?: boolean
  busy?: boolean
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const element = dialog.current!
    const previous = document.activeElement as HTMLElement | null
    element.showModal()
    const original = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      element.close()
      document.body.style.overflow = original
      previous?.focus()
    }
  }, [])
  function dismiss() {
    if (!busy && (!dirty || window.confirm('Discard your unsaved changes?')))
      onClose()
  }
  return (
    <dialog
      className="form-panel"
      ref={dialog}
      aria-labelledby="form-panel-title"
      onCancel={(event) => {
        event.preventDefault()
        dismiss()
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          const r = event.currentTarget.getBoundingClientRect()
          if (
            event.clientX < r.left ||
            event.clientX > r.right ||
            event.clientY < r.top ||
            event.clientY > r.bottom
          )
            dismiss()
        }
      }}
    >
      <header className="form-panel-header">
        <div>
          <p className="eyebrow">SERVICEDESK</p>
          <h2 id="form-panel-title">{title}</h2>
        </div>
        <button
          className="button button-icon button-ghost"
          type="button"
          aria-label="Close form"
          disabled={busy}
          onClick={dismiss}
        >
          <AppIcon name="close" />
        </button>
      </header>
      <div className="form-panel-content">{children}</div>
    </dialog>
  )
}
