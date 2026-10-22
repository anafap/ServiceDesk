export async function problemMessage(response: Response, fallback: string): Promise<string> {
  try {
    const data: unknown = await response.json()
    if (typeof data === 'object' && data !== null && 'detail' in data && typeof data.detail === 'string') return data.detail
  } catch { /* A proxy may return a non-JSON error; retain the actionable fallback. */ }
  return fallback
}
export function validVersion(value: unknown): value is string {
  return typeof value === 'string' && /^[A-Za-z0-9+/]{11}=$/.test(value)
}
