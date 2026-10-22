export function validDisplayNames(value: Record<string, unknown>, fields: string[]) {
  return fields.every(field => value[field] === undefined || value[field] === null || typeof value[field] === 'string')
}
export function displayName(name: string | null | undefined, fallback: string) {
  return name?.trim() || fallback
}
