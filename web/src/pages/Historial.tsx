import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api, type CargaResumen, type Pagina } from '../api'
import EstadoBadge from '../components/EstadoBadge'

const TAMANO = 20

export default function Historial() {
  const [pagina, setPagina] = useState(1)

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['cargas', pagina],
    queryFn: () => api<Pagina<CargaResumen>>(`/api/cargas?pagina=${pagina}&tamano=${TAMANO}`),
    refetchInterval: 5000,                 // polling: los estados cambian solos
    placeholderData: keepPreviousData,     // al cambiar de página no parpadea la tabla
  })

  if (isLoading) return <p>Cargando...</p>
  if (isError) return <p className="error">{error.message}</p>
  if (!data || data.items.length === 0) return <p>Todavía no hay cargas. Sube tu primer Excel.</p>

  const paginas = Math.ceil(data.total / TAMANO)

  return (
    <section className="tarjeta">
      <h1>Historial de cargas</h1>
      <div className="tabla">
        <table>
          <thead>
            <tr><th>#</th><th>Archivo</th><th>Estado</th><th>Resultado</th><th>Subido</th></tr>
          </thead>
          <tbody>
            {data.items.map((c) => (
              <tr key={c.id}>
                <td><Link to={`/cargas/${c.id}`}>{c.id}</Link></td>
                <td>{c.nombreArchivo}</td>
                <td><EstadoBadge estado={c.estado} /></td>
                <td>{c.resultado ?? '-'}</td>
                <td>{new Date(c.fechaRegistro).toLocaleString('es-PE')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="paginacion">
        <button className="secundario" disabled={pagina === 1} onClick={() => setPagina(pagina - 1)}>Anterior</button>
        <span>Página {pagina} de {paginas}</span>
        <button className="secundario" disabled={pagina >= paginas} onClick={() => setPagina(pagina + 1)}>Siguiente</button>
      </div>
    </section>
  )
}