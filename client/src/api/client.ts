import { auth0 } from '@/auth'
import { toSearchParams, type Query } from '@/utils/query'

export class ApiError extends Error {
  status: number
  title: string
  detail: string | undefined
  errors: Record<string, string[]> | undefined

  constructor(status: number, title: string, detail?: string, errors?: Record<string, string[]>) {
    super(detail ?? title)
    this.status = status
    this.title = title
    this.detail = detail
    this.errors = errors
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  form?: FormData
  query?: Query
}

const base = import.meta.env.VITE_API_BASE ?? '/api'
let onError: (e: ApiError) => void = () => {}
/** App.vue wires this to the PrimeVue toast; the client itself has no component context. */
export function setApiErrorHandler(handler: (e: ApiError) => void): void {
  onError = handler
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = await auth0.getAccessTokenSilently()
  const qs = options.query ? toSearchParams(options.query).toString() : ''
  const url = `${base}${path}${qs ? `?${qs}` : ''}`
  const headers: Record<string, string> = { Authorization: `Bearer ${token}` }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  const res = await fetch(url, {
    method: options.method ?? 'GET',
    headers,
    body: options.form ?? (options.body !== undefined ? JSON.stringify(options.body) : undefined),
  })
  if (res.ok) {
    return (res.status === 204 ? undefined : await res.json()) as T
  }

  let problem: Partial<{ title: string; detail: string; errors: Record<string, string[]> }> = {}
  try {
    problem = await res.json()
  } catch {
    // not problem+json; fall back to status text
  }
  const err = new ApiError(res.status, problem.title ?? (res.statusText || `HTTP ${res.status}`), problem.detail, problem.errors)
  onError(err)
  throw err
}
