export type AuthUser = { id: number; name: string; role: 'Admin' | 'StoreManager'; storeId: number | null; storeName?: string | null }
export type AuthSession = { accessToken: string; expiresAt: string; user: AuthUser }

let session: AuthSession | null = null
const listeners = new Set<() => void>()

export function getSession() { return session }

export function setSession(next: AuthSession | null) {
  session = next
  listeners.forEach((listener) => listener())
}

export function subscribeSession(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}
