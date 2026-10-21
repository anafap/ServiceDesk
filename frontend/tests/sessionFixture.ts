import { setSession } from '../src/auth/session.ts'

export function signInForTest() {
  setSession({ accessToken: 'test-access-token', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(),
    user: { id: 1, name: 'Test Admin', role: 'Admin', storeId: null } })
}
