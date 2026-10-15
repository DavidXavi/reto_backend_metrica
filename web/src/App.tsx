import { Navigate, Route, Routes } from 'react-router'
import Layout from './components/Layout'
import Login from './pages/Login'
import Subir from './pages/Subir'
import Historial from './pages/Historial'
import Detalle from './pages/Detalle'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      {/* Layout revisa la sesión: sin token, manda al login */}
      <Route element={<Layout />}>
        <Route index element={<Historial />} />
        <Route path="subir" element={<Subir />} />
        <Route path="cargas/:id" element={<Detalle />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}