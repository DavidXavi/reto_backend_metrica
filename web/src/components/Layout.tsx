import { Link, Navigate, Outlet, useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { sesion } from '../api'

export default function Layout() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const s = sesion.leer()
  if (!s) return <Navigate to="/login" replace />

  function salir() {
    sesion.cerrar()
    queryClient.clear()        // no dejar datos del usuario anterior en caché
    navigate('/login')
  }

  return (
    <>
      <header className="barra">
        <strong>Carga masiva</strong>
        <nav>
          <Link to="/">Historial</Link>
          {s.rol === 'Cargador' && <Link to="/subir">Subir Excel</Link>}
        </nav>
        <span className="usuario">{s.email}</span>
        <button className="secundario" onClick={salir}>Salir</button>
      </header>
      <main>
        <Outlet />
      </main>
    </>
  )
}