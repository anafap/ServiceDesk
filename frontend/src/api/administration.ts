import { apiFetch } from './http.ts'
import { problemMessage, validVersion } from './problems.ts'

export type ResourceKind = 'stores' | 'external-technicians' | 'store-managers'
export type AdminRecord = { id: number; isActive: boolean; version: string } & Record<string, string | number | boolean | null>
export type AdminField = { key: string; label: string; maxLength: number; type?: 'email' | 'store' }
export const resourceFields: Record<ResourceKind, AdminField[]> = {
  stores: [{ key: 'storeCode', label: 'Store code', maxLength: 50 }, { key: 'name', label: 'Store name', maxLength: 50 }, { key: 'address', label: 'Address', maxLength: 50 }, { key: 'city', label: 'City', maxLength: 500 }],
  'external-technicians': [{ key: 'fullName', label: 'Full name', maxLength: 200 }, { key: 'companyName', label: 'Company name', maxLength: 200 }, { key: 'phoneNumber', label: 'Phone number', maxLength: 20 }, { key: 'specialisation', label: 'Specialisation', maxLength: 200 }],
  'store-managers': [{ key: 'firstName', label: 'First name', maxLength: 100 }, { key: 'lastName', label: 'Last name', maxLength: 100 }, { key: 'email', label: 'Email', type: 'email', maxLength: 320 }, { key: 'phoneNumber', label: 'Phone number', maxLength: 50 }, { key: 'storeId', label: 'Assigned store', type: 'store', maxLength: 20 }],
}
export function validateAdminInput(kind: ResourceKind, input: Record<string, string>) {
  const errors = Object.fromEntries(resourceFields[kind].filter(field => !input[field.key]?.trim() || input[field.key].trim().length > field.maxLength)
    .map(field => [field.key, `${field.label} is required (maximum ${field.maxLength} characters).`]))
  if (kind === 'store-managers') {
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(input.email?.trim() ?? '')) errors.email = 'Enter a valid email address.'
    if (!/^[1-9]\d*$/.test(input.storeId ?? '')) errors.storeId = 'Select an active store.'
  }
  return errors
}
export async function listAdminRecords(kind: ResourceKind, signal: AbortSignal): Promise<AdminRecord[]> {
  const response = await apiFetch(`/api/admin/${kind}`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error(await problemMessage(response, `Records could not be loaded (HTTP ${response.status}).`))
  const data: unknown = await response.json()
  if (!Array.isArray(data) || !data.every((item: unknown) => {
    if (typeof item !== 'object' || item === null) return false
    const record = item as Record<string, unknown>
    return Number.isInteger(record.id) && Number(record.id) > 0 && typeof record.isActive === 'boolean' && validVersion(record.version)
      && resourceFields[kind].every(field => field.type === 'store' ? Number.isInteger(record[field.key]) && Number(record[field.key]) > 0 : typeof record[field.key] === 'string')
  })) throw new Error('Unexpected administration data.')
  return data as AdminRecord[]
}
async function write(path: string, method: string, input: Record<string, string | number | boolean>, signal: AbortSignal) {
  let response: Response
  const uncertain = 'We could not confirm the save. Refresh the list before trying again.'
  try {
    response = await apiFetch(path, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input), signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  } catch { throw new Error(uncertain) }
  if (response.ok) return
  if (response.status >= 500) throw new Error(uncertain)
  throw new Error(await problemMessage(response, response.status === 409 ? 'This record changed or a unique value is already in use. Refresh before trying again.' : 'The save was rejected. Check the fields and your permissions.'))
}
export function saveAdminRecord(kind: ResourceKind, id: number | null, input: Record<string, string | number | boolean>, signal: AbortSignal, activation = false) {
  return write(`/api/admin/${kind}${id === null ? '' : `/${id}`}${activation ? '/status' : ''}`, activation ? 'PATCH' : id === null ? 'POST' : 'PUT', input, signal)
}
export function resetManagerPassword(id: number, password: string, expectedVersion: string, signal: AbortSignal) {
  return write(`/api/admin/store-managers/${id}/reset-password`, 'POST', { password, expectedVersion }, signal)
}
