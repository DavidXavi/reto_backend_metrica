// Todo el HTTP pasa por aquí: agrega el token, renueva la sesión con el
// refresh token cuando recibe 401 y convierte los ProblemDetails en errores.
const API = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

export type Sesion = { accessToken: string; refreshToken: string; email: string; rol: string }

const CLAVE = 'sesion'
export const sesion = {
  leer(): Sesion | null {
    try { return JSON.parse(localStorage.getItem(CLAVE) ?? 'null') } catch { return null }
  },
  guardar(s: Sesion) { localStorage.setItem(CLAVE, JSON.stringify(s)) },
  cerrar() { localStorage.removeItem(CLAVE) },
}

export class ApiError extends Error {
  status: number
  constructor(status: number, mensaje: string) {
    super(mensaje)
    this.status = status
  }
}

async function leerError(r: Response): Promise<string> {
  if (r.status === 429) return 'Demasiadas peticiones, espera un minuto'
  try {
    const p = await r.json()
    return p.detail ?? p.title ?? r.statusText
  } catch {
    return r.statusText
  }
}

export async function login(email: string, password: string): Promise<Sesion> {
  const r = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!r.ok) throw new ApiError(r.status, await leerError(r))
  const s: Sesion = await r.json()
  sesion.guardar(s)
  return s
}

// Si varias peticiones reciben 401 a la vez, todas esperan la misma renovación
let renovando: Promise<boolean> | null = null
function renovar(): Promise<boolean> {
  renovando ??= (async () => {
    const s = sesion.leer()
    if (!s) return false
    const r = await fetch(`${API}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: s.refreshToken }),
    })
    if (!r.ok) { sesion.cerrar(); return false }
    sesion.guardar(await r.json())
    return true
  })().finally(() => { renovando = null })
  return renovando
}

async function pedir(ruta: string, init: RequestInit = {}, reintento = true): Promise<Response> {
  const s = sesion.leer()
  const r = await fetch(`${API}${ruta}`, {
    ...init,
    headers: { ...init.headers, ...(s ? { Authorization: `Bearer ${s.accessToken}` } : {}) },
  })
  if (r.status === 401 && reintento && (await renovar())) return pedir(ruta, init, false)
  if (r.status === 401) {
    sesion.cerrar()
    window.location.assign('/login')
  }
  if (!r.ok) throw new ApiError(r.status, await leerError(r))
  return r
}

export async function api<T>(ruta: string, init?: RequestInit): Promise<T> {
  return (await pedir(ruta, init)).json() as Promise<T>
}

export async function descargar(ruta: string, nombre: string) {
  const blob = await (await pedir(ruta)).blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = nombre
  a.click()
  URL.revokeObjectURL(url)
}

// ---------- tipos que devuelve el backend ----------
export type Estado = 'Pendiente' | 'EnProceso' | 'Cargado' | 'Finalizado' | 'Notificado'

export type CargaResumen = {
  id: number; nombreArchivo: string; estado: Estado; resultado: string | null
  fechaRegistro: string; fechaFin: string | null
}

export type Pagina<T> = { items: T[]; paginaActual: number; tamano: number; total: number }

export type CargaDetalle = CargaResumen & {
  tamanoBytes: number; usuario: string
  totalFilas: number | null; insertadas: number | null; existentes: number | null; invalidas: number | null
  historial: { estado: Estado; servicio: string; detalle: string | null; fecha: string }[]
}

export type Registro = {
  filaExcel: number; periodo: string; codigoProducto: string; nombreProducto: string; precio: number
}

export type Fallo = {
  filaExcel: number | null; periodo: string | null; codigoProducto: string | null; motivo: string; detalle: string | null
}