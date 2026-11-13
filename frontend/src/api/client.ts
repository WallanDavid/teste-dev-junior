import type { ProblemDetails } from './types'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

/** Erro HTTP com o ProblemDetails da API já interpretado. */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails) {
    super(ApiError.mensagem(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.code = problem.code
    this.fieldErrors = problem.errors ?? {}
  }

  private static mensagem(status: number, problem: ProblemDetails): string {
    const campos = Object.values(problem.errors ?? {}).flat()
    if (campos.length > 0) return campos.join(' ')
    return problem.detail ?? problem.title ?? `Erro ${status} ao comunicar com o servidor.`
  }
}

type Query = Record<string, string | number | undefined | null>

function montarUrl(path: string, query?: Query): string {
  const params = new URLSearchParams()
  for (const [chave, valor] of Object.entries(query ?? {})) {
    if (valor !== undefined && valor !== null && valor !== '') params.set(chave, String(valor))
  }
  const qs = params.toString()
  return `${BASE_URL}${path}${qs ? `?${qs}` : ''}`
}

async function request<T>(method: string, path: string, options: { query?: Query; body?: unknown; signal?: AbortSignal } = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(montarUrl(path, options.query), {
      method,
      signal: options.signal,
      headers: options.body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    })
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e
    throw new ApiError(0, { detail: 'Não foi possível conectar à API. Verifique se ela está no ar.' })
  }

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}))
    throw new ApiError(response.status, problem)
  }
  return (await response.json()) as T
}

export const http = {
  get: <T>(path: string, query?: Query, signal?: AbortSignal) => request<T>('GET', path, { query, signal }),
  post: <T>(path: string, body: unknown) => request<T>('POST', path, { body }),
}
