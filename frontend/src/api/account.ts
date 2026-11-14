import { apiFetch } from './http.ts'
import { validVersion } from './problems.ts'
import { problemMessage } from './problems.ts'
export async function changePassword(
  currentPassword: string,
  newPassword: string,
  signal: AbortSignal,
) {
  let response: Response
  try {
    response = await apiFetch('/api/account/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch {
    throw new Error(
      'We could not confirm the password change. Try signing in with your new password before retrying.',
    )
  }
  if (response.status >= 500)
    throw new Error(
      'We could not confirm the password change. Try signing in with your new password before retrying.',
    )
  if (!response.ok)
    throw new Error(
      await problemMessage(response, 'Password change was not accepted.'),
    )
}

export type AccountProfile = {
  id: number
  firstName: string
  lastName: string
  email: string
  phoneNumber: string
  role: 'Admin' | 'StoreManager'
  storeId: number | null
  storeName: string | null
  version: string
}
function parseProfile(data: unknown): AccountProfile {
  if (!data || typeof data !== 'object')
    throw new Error('Unexpected account response.')
  const p = data as Record<string, unknown>
  if (
    !Number.isInteger(p.id) ||
    Number(p.id) <= 0 ||
    !validVersion(p.version) ||
    !(
      p.storeId === null ||
      (Number.isInteger(p.storeId) && Number(p.storeId) > 0)
    ) ||
    !(p.storeName === null || typeof p.storeName === 'string') ||
    !['firstName', 'lastName', 'email', 'phoneNumber', 'version'].every(
      (k) => typeof p[k] === 'string',
    ) ||
    !['Admin', 'StoreManager'].includes(String(p.role))
  )
    throw new Error('Unexpected account response.')
  return data as AccountProfile
}
export async function getAccount(signal: AbortSignal): Promise<AccountProfile> {
  const response = await apiFetch('/api/account', {
    signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
  })
  if (!response.ok)
    throw new Error(
      await problemMessage(response, 'Your account could not be loaded.'),
    )
  return parseProfile(await response.json())
}
export async function updateProfile(
  input: Pick<AccountProfile, 'firstName' | 'lastName' | 'phoneNumber'> & {
    expectedVersion: string
  },
  signal: AbortSignal,
): Promise<AccountProfile> {
  let response: Response
  try {
    response = await apiFetch('/api/account/profile', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch {
    throw new Error(
      'We could not confirm the save. Reload your profile before trying again.',
    )
  }
  if (!response.ok)
    throw new Error(
      await problemMessage(
        response,
        response.status === 409
          ? 'Your profile changed elsewhere. Reload it before saving again.'
          : 'Your profile could not be saved.',
      ),
    )
  return parseProfile(await response.json())
}
