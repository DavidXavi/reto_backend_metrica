import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { api } from '../api'

const MAXIMO_MB = 10

export default function Subir() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [archivo, setArchivo] = useState<File | null>(null)

  const mutation = useMutation({
    mutationFn: (f: File) => {
      const form = new FormData()
      form.append('archivo', f)                 // el nombre del campo que espera Control
      return api<{ idCarga: number }>('/api/cargas', { method: 'POST', body: form })
    },
    onSuccess: ({ idCarga }) => {
      queryClient.invalidateQueries({ queryKey: ['cargas'] })
      navigate(`/cargas/${idCarga}`)
    },
  })

  // Validar en el cliente es comodidad para el usuario. La validación real está en el backend.
  const muyGrande = archivo !== null && archivo.size > MAXIMO_MB * 1024 * 1024

  return (
    <section className="tarjeta">
      <h1>Subir Excel</h1>
      <p>Columnas: Periodo, CodigoProducto, NombreProducto, Precio. Máximo {MAXIMO_MB} MB.</p>
      <input
        type="file"
        accept=".xlsx"
        onChange={(e) => { setArchivo(e.target.files?.[0] ?? null); mutation.reset() }}
      />
      {muyGrande && <p className="error">El archivo pasa de {MAXIMO_MB} MB.</p>}
      {mutation.isError && <p className="error">{mutation.error.message}</p>}
      <button
        disabled={!archivo || muyGrande || mutation.isPending}
        onClick={() => archivo && mutation.mutate(archivo)}
      >
        {mutation.isPending ? 'Subiendo...' : 'Subir'}
      </button>
    </section>
  )
}