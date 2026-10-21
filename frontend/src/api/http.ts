import { getSession, setSession } from '../auth/session.ts'

export async function apiFetch(path: string, options: RequestInit = {}) {
  const session = getSession()
  if (!session || Date.parse(session.expiresAt) <= Date.now()) {
    setSession(null)
    throw new Error('Please sign in to continue.')
  }
  const headers = new Headers(options.headers)
  headers.set('Authorization', `Bearer ${session.accessToken}`)
  const response = await fetch(path, { ...options, headers })
  if (response.status === 401 && getSession()?.accessToken === session.accessToken) {
    setSession(null)
    throw new Error('Your session has expired. Please sign in again.')
  }
  return response
}
