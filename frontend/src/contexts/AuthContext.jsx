import { createContext, useCallback, useContext, useMemo, useState } from 'react'

const TOKEN_KEY = 'ior-auth-token'
const USER_KEY = 'ior-auth-user'

const AuthContext = createContext(null)

/**
 * Decode the JWT payload without verifying the signature.
 * Verification happens on the server on every request.
 */
function decodeToken(token) {
  try {
    const payload = token.split('.')[1]
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')))
  } catch {
    return null
  }
}

function isTokenExpired(token) {
  const payload = decodeToken(token)
  if (!payload?.exp) return true
  return Date.now() >= payload.exp * 1000
}

function loadStoredAuth() {
  try {
    const token = localStorage.getItem(TOKEN_KEY)
    const user = JSON.parse(localStorage.getItem(USER_KEY) ?? 'null')
    if (token && user && !isTokenExpired(token)) {
      return { token, user }
    }
  } catch {
    // corrupted storage — clear it
  }
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
  return { token: null, user: null }
}

export function AuthProvider({ children }) {
  const stored = useMemo(() => loadStoredAuth(), [])
  const [token, setToken] = useState(stored.token)
  const [user, setUser] = useState(stored.user)

  const login = useCallback((tokenValue, userData) => {
    localStorage.setItem(TOKEN_KEY, tokenValue)
    localStorage.setItem(USER_KEY, JSON.stringify(userData))
    setToken(tokenValue)
    setUser(userData)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    setToken(null)
    setUser(null)
  }, [])

  const value = useMemo(
    () => ({ token, user, isAuthenticated: Boolean(token), login, logout }),
    [token, user, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}

/** Module-level fetch wrapper — reads the token fresh on every call. */
export function apiFetch(url, options = {}) {
  const token = localStorage.getItem(TOKEN_KEY)
  return fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })
}
