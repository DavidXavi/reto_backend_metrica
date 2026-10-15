import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { login } from '../api'

export default function Login() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('cargador@reto.pe')
  const [password, setPassword] = useState('')

  const mutation = useMutation({
    mutationFn: () => login(email, password),
    onSuccess: () => navigate('/'),
  })

  return (
    <main className="centro">
      <form
        className="tarjeta"
        onSubmit={(e) => { e.preventDefault(); mutation.mutate() }}
      >
        <h1>Iniciar sesión</h1>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>
        <label>
          Contraseña
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {mutation.isError && <p className="error">{mutation.error.message}</p>}
        <button disabled={mutation.isPending}>{mutation.isPending ? 'Ingresando...' : 'Ingresar'}</button>
      </form>
    </main>
  )
}