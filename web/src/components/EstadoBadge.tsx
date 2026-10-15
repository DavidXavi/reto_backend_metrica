import type { Estado } from '../api'

const textos: Record<Estado, string> = {
  Pendiente: 'Pendiente',
  EnProceso: 'En proceso',
  Cargado: 'Cargado',
  Finalizado: 'Finalizado',
  Notificado: 'Notificado',
}

export default function EstadoBadge({ estado }: { estado: Estado }) {
  return <span className={`badge badge-${estado}`}>{textos[estado]}</span>
}