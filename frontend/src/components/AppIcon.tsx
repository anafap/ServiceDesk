type IconName = 'brand' | 'overview' | 'tickets' | 'arrow' | 'info'

const paths: Record<IconName, string> = {
  brand: 'M8 6h11v12H8M5 9h6M5 13h6M5 17h6',
  overview: 'M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z',
  tickets: 'M4 5h16v5a2 2 0 0 0 0 4v5H4v-5a2 2 0 0 0 0-4zM14 8v2m0 4v2',
  arrow: 'M5 12h14m-6-6 6 6-6 6',
  info: 'M12 11v6m0-10v.01M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
}

export function AppIcon({ name }: { name: IconName }) {
  return (
    <svg className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={paths[name]} />
    </svg>
  )
}
