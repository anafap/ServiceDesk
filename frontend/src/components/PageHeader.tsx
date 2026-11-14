import type { ReactNode } from 'react'
type PageHeaderProps = {
  actions?: ReactNode
  eyebrow: string
  title: string
  description: string
}

export function PageHeader({ eyebrow, title, description, actions }: PageHeaderProps) {
  return (
    <div className="page-header">
      <div><p className="eyebrow">{eyebrow}</p>
      <h1>{title}</h1>
      <p className="page-description">{description}</p>
    </div>{actions && <div className="header-actions">{actions}</div>}</div>
  )
}
