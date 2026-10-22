import { apiFetch } from './http.ts'

export type ResourceKind = 'stores' | 'external-technicians'
export type AdminRecord = { id: number; isActive: boolean } & Record<string, string | number | boolean>
export type AdminField = { key: string; label: string; maxLength: number }
export const resourceFields: Record<ResourceKind, AdminField[]> = {
  stores: [{ key: 'storeCode', label: 'Store code', maxLength: 50 }, { key: 'name', label: 'Store name', maxLength: 50 }, { key: 'address', label: 'Address', maxLength: 50 }, { key: 'city', label: 'City', maxLength: 500 }],
  'external-technicians': [{ key: 'fullName', label: 'Full name', maxLength: 200 }, { key: 'companyName', label: 'Company name', maxLength: 200 }, { key: 'phoneNumber', label: 'Phone number', maxLength: 20 }, { key: 'specialisation', label: 'Specialisation', maxLength: 200 }],
}
export function validateAdminInput(kind: ResourceKind, input: Record<string, string>) {
  return Object.fromEntries(resourceFields[kind].filter(field => !input[field.key]?.trim() || input[field.key].trim().length > field.maxLength)
    .map(field => [field.key, `${field.label} is required (maximum ${field.maxLength} characters).`]))
}
export async function listAdminRecords(kind: ResourceKind, signal: AbortSignal): Promise<AdminRecord[]> {
  const response = await apiFetch(`/api/admin/${kind}`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]) })
  if (!response.ok) throw new Error(`Records could not be loaded (HTTP ${response.status}).`)
  const data: unknown = await response.json()
  if (!Array.isArray(data) || !data.every(item => typeof item === 'object' && item !== null
    && Number.isInteger(item.id) && item.id > 0 && typeof item.isActive === 'boolean'
    && resourceFields[kind].every(field => typeof item[field.key] === 'string'))) throw new Error('Unexpected administration data.')
  return data as AdminRecord[]
}
export async function saveAdminRecord(kind: ResourceKind, id: number | null, input: Record<string, string> | { isActive: boolean }, signal: AbortSignal, activation = false): Promise<void> {
  let response: Response
  const uncertain = 'We could not confirm the save. Refresh the list before trying again.'
  try {
    response = await apiFetch(`/api/admin/${kind}${id === null ? '' : `/${id}`}${activation ? '/status' : ''}`, {
      method: activation ? 'PATCH' : id === null ? 'POST' : 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input),
      signal: AbortSignal.any([signal, AbortSignal.timeout(15_000)]),
    })
  } catch { throw new Error(uncertain) }
  if (response.ok) return
  if (response.status >= 500) throw new Error(uncertain)
  if (response.status === 409) throw new Error('This store code is already in use. Choose another code.')
  if (response.status === 403) throw new Error('Only administrators can manage these records.')
  if (response.status === 404) throw new Error('This record no longer exists. Refresh the list.')
  throw new Error('The save was rejected. Check the required fields and length limits.')
}
