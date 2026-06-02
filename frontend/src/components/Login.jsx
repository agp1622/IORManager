import { useState } from 'react'
import { useAuth } from '../contexts/AuthContext'
import PapavelagLogo from '../assets/papavelag-logo.svg'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5031/api'

export default function Login() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [isLoading, setIsLoading] = useState(false)

  const handleSubmit = async (event) => {
    event.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      const response = await fetch(`${API_BASE_URL}/Auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })

      const body = await response.json().catch(() => null)

      if (!response.ok) {
        throw new Error(body?.message || 'Credenciales incorrectas.')
      }

      login(body.token, { name: body.name, email: body.email, role: body.role })
    } catch (err) {
      setError(err.message || 'Error al iniciar sesión.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-card__brand">
          <img src={PapavelagLogo} alt="Papavelag Technologies logo" className="login-card__logo" />
          <p className="brand-panel__name">PAPAVELAG</p>
          <p className="brand-panel__tagline">Technologies Y Soluciones S.R.L.</p>
        </div>

        <h1 className="login-card__title">Iniciar sesión</h1>

        <form onSubmit={handleSubmit} className="login-card__form">
          {error ? (
            <div className="banner banner--error" role="alert">
              {error}
            </div>
          ) : null}

          <label className="modal-input">
            Correo electrónico
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="usuario@empresa.com"
              required
              autoComplete="email"
              autoFocus
              disabled={isLoading}
            />
          </label>

          <label className="modal-input">
            Contraseña
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              required
              autoComplete="current-password"
              disabled={isLoading}
            />
          </label>

          <button
            type="submit"
            className="button login-card__submit"
            disabled={isLoading || !email || !password}
          >
            {isLoading ? 'Iniciando sesión…' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}
