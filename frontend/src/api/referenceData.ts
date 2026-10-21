export type ReferenceItem = { id: number; name: string }

export async function getReferenceData(signal: AbortSignal): Promise<{
  stores: ReferenceItem[]
  categories: ReferenceItem[]
}> {
  async function load(path: string): Promise<ReferenceItem[]> {
    const response = await fetch(path, {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
    if (!response.ok) throw new Error('Stores and categories could not be loaded. Please try again.')
    const data: unknown = await response.json()
    if (!Array.isArray(data) || !data.every((item: unknown) => {
      if (typeof item !== 'object' || item === null) return false
      const entry = item as Record<string, unknown>
      return typeof entry.id === 'number' && Number.isInteger(entry.id) && entry.id > 0
        && typeof entry.name === 'string' && entry.name.trim().length > 0
    })) throw new Error('The server returned unexpected store or category data.')
    return data as ReferenceItem[]
  }

  const [stores, categories] = await Promise.all([load('/api/stores'), load('/api/categories')])
  return { stores, categories }
}
