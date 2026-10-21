import type { ReactNode } from 'react'
import { AppIcon } from './AppIcon'

type EmptyStateProps = {
  icon: 'overview' | 'tickets'
  title: string
  description: string
  children?: ReactNode
}

export function EmptyState({ icon, title, description, children }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <span className="empty-icon"><AppIcon name={icon} /></span>
      <h3>{title}</h3>
      <p>{description}</p>
      {children}
    </div>
  )
}
