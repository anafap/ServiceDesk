import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'

export function ActionMenu({
  label,
  children,
  className = '',
  trigger,
}: {
  label: string
  children: ReactNode
  className?: string
  trigger?: ReactNode
}) {
  const [position, setPosition] = useState({ top: 0, right: 0 })
  const element = useRef<HTMLDetailsElement>(null)
  useEffect(() => {
    function outside(event: MouseEvent) {
      if (
        !(event.target instanceof Node) ||
        !element.current?.contains(event.target)
      )
        element.current?.removeAttribute('open')
    }
    function escape(event: KeyboardEvent) {
      if (event.key === 'Escape' && element.current?.open) {
        element.current.removeAttribute('open')
        element.current.querySelector('summary')?.focus()
      }
    }
    const scroll = () => {
      if (!element.current?.open) return
      const rect = element.current.getBoundingClientRect()
      if (rect.bottom < 0 || rect.top > window.innerHeight) {
        element.current.removeAttribute('open')
        return
      }
      setPosition({
        top: Math.min(rect.bottom + 8, window.innerHeight - 200),
        right: Math.max(12, window.innerWidth - rect.right),
      })
    }
    window.addEventListener('resize', scroll)
    document.addEventListener('scroll', scroll, true)
    document.addEventListener('click', outside)
    document.addEventListener('keydown', escape)
    return () => {
      window.removeEventListener('resize', scroll)
      document.removeEventListener('scroll', scroll, true)
      document.removeEventListener('click', outside)
      document.removeEventListener('keydown', escape)
    }
  }, [])
  return (
    <details
      className={`action-menu ${className}`}
      ref={element}
      onToggle={() => {
        if (element.current?.open) {
          const rect = element.current.getBoundingClientRect()
          setPosition({
            top: Math.min(rect.bottom + 8, window.innerHeight - 200),
            right: Math.max(12, window.innerWidth - rect.right),
          })
        }
      }}
    >
      <summary aria-label={label}>{trigger ?? '•••'}</summary>
      <div
        className="action-menu-content"
        style={{ position: 'fixed', top: position.top, right: position.right }}
        onClick={(event) => {
          if ((event.target as Element).closest('button,a'))
            element.current?.removeAttribute('open')
        }}
      >
        {children}
      </div>
    </details>
  )
}
