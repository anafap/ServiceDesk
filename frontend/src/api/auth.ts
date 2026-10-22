import { problemMessage } from './problems.ts'
import { validDisplayNames } from '../utils/displayNames.ts'
import type { AuthSession } from '../auth/session'

export async function login(email: string, password: string, signal: AbortSignal): Promise<AuthSession> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: email.trim(), password }),
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })
  if (response.status === 429) throw new Error(`${await problemMessage(response, 'Too many sign-in attempts.')} Try again in ${response.headers.get('Retry-After') ?? '60'} seconds.`)
  if (response.status === 401) throw new Error('Email or password is incorrect.')
  if (!response.ok) throw new Error('Sign-in is unavailable. Please try again.')
  const data: unknown = await response.json()
  if (typeof data !== 'object' || data === null) throw new Error('Unexpected sign-in response.')
  const result = data as Record<string, unknown>
  if (typeof result.accessToken !== 'string' || !result.accessToken || result.tokenType !== 'Bearer'
    || typeof result.expiresAt !== 'string' || !(Date.parse(result.expiresAt) > Date.now())
    || typeof result.user !== 'object' || result.user === null) throw new Error('Unexpected sign-in response.')
  const user = result.user as Record<string, unknown>
  if (!validDisplayNames(user, ['storeName']) || typeof user.id !== 'number' || !Number.isInteger(user.id) || user.id <= 0 || typeof user.name !== 'string'
    || !['Admin', 'StoreManager'].includes(String(user.role))
    || (user.role === 'Admin' ? user.storeId !== null : typeof user.storeId !== 'number' || !Number.isInteger(user.storeId) || user.storeId <= 0)) {
    throw new Error('Unexpected sign-in response.')
  }
  return result as unknown as AuthSession
}
