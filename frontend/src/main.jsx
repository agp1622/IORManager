import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.jsx'
import Login from './components/Login.jsx'
import { AuthProvider, useAuth } from './contexts/AuthContext.jsx'
import './index.css'

function Root() {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <App /> : <Login />
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <AuthProvider>
      <Root />
    </AuthProvider>
  </React.StrictMode>,
)
