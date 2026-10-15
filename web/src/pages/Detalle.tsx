import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router'
import { api, descargar, type CargaDetalle, type Fallo, type Registro } from '../api'
import EstadoBadge from '../components/EstadoBadge'

export default function Detalle() {
  const { id } = useParams()
  const [pestana, setPestana] = useState<'registros' | 'fallos'>('registros')

  const carga = useQuery({
    queryKey: ['carga', id],
    queryFn: () => api<CargaDetalle>(`/api/cargas/${id}`),
    // Polling cada 2 s hasta el estado final; ahí se apaga solo
    refetchInterval: (q) => (q.state.data?.estado === 'Notificado' ? false : 2000),
  })

  const terminada = carga.data?.estado === 'Finalizado' || carga.data?.estado === 'Notificado'

  const registros = useQuery({
    queryKey: ['carga', id, 'registros'],
    queryFn: () => api<Registro[]>(`/api/datos/cargas/${id}/registros`),
    enabled: terminada && pestana === 'registros',
  })

  const fallos = useQuery({
    queryKey: ['carga', id, 'fallos'],
    queryFn: () => api<Fallo[]>(`/api/datos/cargas/${id}/fallos`),
    enabled: terminada && pestana === 'fallos',
  })

  if (carga.isLoading) return <p>Cargando...</p>
  if (carga.isError) return <p className="error">{carga.error.message}</p>
  const c = carga.data!

  return (
    <section className="tarjeta">
      <h1>Carga {c.id}: {c.nombreArchivo}</h1>
      <p><EstadoBadge estado={c.estado} /> {c.resultado && <strong>{c.resultado}</strong>}</p>

      <div className="resumen">
        <div><span>Filas</span><b>{c.totalFilas ?? '-'}</b></div>
        <div><span>Insertadas</span><b>{c.insertadas ?? '-'}</b></div>
        <div><span>Existentes</span><b>{c.existentes ?? '-'}</b></div>
        <div><span>Inválidas</span><b>{c.invalidas ?? '-'}</b></div>
      </div>

      <h2>Trazabilidad</h2>
      <ol className="linea">
        {c.historial.map((h, i) => (
          <li key={i}>
            <EstadoBadge estado={h.estado} /> {new Date(h.fecha).toLocaleTimeString('es-PE')} por {h.servicio}
            {h.detalle && <small> ({h.detalle})</small>}
          </li>
        ))}
      </ol>

      <button className="secundario" onClick={() => descargar(`/api/cargas/${c.id}/archivo`, c.nombreArchivo)}>
        Descargar Excel original
      </button>

      {terminada && (
        <>
          <div className="pestanas">
            <button className={pestana === 'registros' ? '' : 'secundario'} onClick={() => setPestana('registros')}>
              Registros
            </button>
            <button className={pestana === 'fallos' ? '' : 'secundario'} onClick={() => setPestana('fallos')}>
              Fallos
            </button>
          </div>

          {pestana === 'registros' && (
            <Tabla
              cabecera={['Fila', 'Periodo', 'Código', 'Nombre', 'Precio']}
              filas={registros.data?.map((r) => [r.filaExcel, r.periodo, r.codigoProducto, r.nombreProducto, r.precio.toFixed(2)])}
            />
          )}
          {pestana === 'fallos' && (
            <Tabla
              cabecera={['Fila', 'Periodo', 'Código', 'Motivo', 'Detalle']}
              filas={fallos.data?.map((f) => [f.filaExcel ?? '-', f.periodo ?? '-', f.codigoProducto ?? '-', f.motivo, f.detalle ?? ''])}
            />
          )}
        </>
      )}
    </section>
  )
}

function Tabla({ cabecera, filas }: { cabecera: string[]; filas?: (string | number)[][] }) {
  if (!filas) return <p>Cargando...</p>
  if (filas.length === 0) return <p>Sin filas.</p>
  return (
    <div className="tabla tabla-scroll">
      <table>
        <thead><tr>{cabecera.map((h) => <th key={h}>{h}</th>)}</tr></thead>
        <tbody>
          {filas.map((f, i) => <tr key={i}>{f.map((v, j) => <td key={j}>{v}</td>)}</tr>)}
        </tbody>
      </table>
    </div>
  )
}