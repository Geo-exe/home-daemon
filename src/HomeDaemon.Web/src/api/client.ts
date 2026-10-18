/**
 * The single HTTP entry point. It keeps the antiforgery token in memory, sends the session
 * cookie with every request and turns Problem Details responses into {@link ApiError}.
 *
 * No token is ever written to storage: the session lives in an HttpOnly cookie the script
 * cannot read.
 */

const API_ROOT = '/api/v1'

/** Raised for any non-successful API response. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly problems?: Record<string, string[]>,
  ) {
    super(message)
    this.name = 'ApiError'
  }

  get isUnauthorized(): boolean {
    return this.status === 401
  }

  get isForbidden(): boolean {
    return this.status === 403
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  get isConflict(): boolean {
    return this.status === 409
  }
}

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

interface RequestOptions {
  body?: unknown
  signal?: AbortSignal
}

let antiforgeryToken: string | null = null

/** Drops the cached token, for example after signing in or out. */
export function resetAntiforgeryToken(): void {
  antiforgeryToken = null
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, { signal })
}

export function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return request<T>('POST', path, { body })
}

export function apiPut<T>(path: string, body?: unknown): Promise<T> {
  return request<T>('PUT', path, { body })
}

export function apiPatch<T>(path: string, body?: unknown): Promise<T> {
  return request<T>('PATCH', path, { body })
}

export function apiDelete(path: string): Promise<undefined> {
  return request<undefined>('DELETE', path, {})
}

async function request<T>(method: Method, path: string, options: RequestOptions, isRetry = false): Promise<T> {
  const headers = new Headers()

  let payload: string | undefined
  if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json')
    payload = JSON.stringify(options.body)
  }

  if (method !== 'GET') {
    headers.set('X-CSRF-TOKEN', await ensureAntiforgeryToken())
  }

  const response = await fetch(`${API_ROOT}${path}`, {
    method,
    headers,
    body: payload,
    credentials: 'same-origin',
    signal: options.signal ?? null,
  })

  const text = await response.text()

  if (!response.ok) {
    // The antiforgery cookie and the cached token can drift apart, for example after the
    // cookie expires. Fetch a fresh pair once and replay the request.
    if (response.status === 400 && !isRetry && text.includes('antiforgery')) {
      antiforgeryToken = null
      return request<T>(method, path, options, true)
    }

    throw toApiError(response, text)
  }

  return (text ? (JSON.parse(text) as T) : undefined) as T
}

async function ensureAntiforgeryToken(): Promise<string> {
  if (antiforgeryToken !== null) {
    return antiforgeryToken
  }

  const response = await fetch(`${API_ROOT}/auth/csrf`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw toApiError(response, await response.text())
  }

  const payload = (await response.json()) as { token: string }
  antiforgeryToken = payload.token
  return payload.token
}

function toApiError(response: Response, text: string): ApiError {
  let detail = response.statusText || `Request failed with status ${response.status}`
  let problems: Record<string, string[]> | undefined

  if (text) {
    try {
      const problem = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[]> }
      detail = problem.detail ?? problem.title ?? detail
      problems = problem.errors
    } catch {
      // Not a Problem Details payload; keep the status text.
    }
  }

  return new ApiError(response.status, detail, problems)
}
