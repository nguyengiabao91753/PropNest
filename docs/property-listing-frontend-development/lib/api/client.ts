/**
 * Base HTTP Client for PropNest ASP.NET Core Backend
 * Connects to http://localhost:5000/api/v1 by default
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api/v1'

export class ProblemDetailsError extends Error {
  status: number
  title?: string
  detail?: string
  errors?: Record<string, string[]>

  constructor(status: number, message: string, data?: any) {
    super(message)
    this.name = 'ProblemDetailsError'
    this.status = status
    this.title = data?.title
    this.detail = data?.detail
    this.errors = data?.errors
  }
}

export async function apiFetch<T>(
  endpoint: string,
  options: RequestInit & { idempotencyKey?: string } = {},
): Promise<T> {
  const token = typeof window !== 'undefined' ? localStorage.getItem('propnest_access_token') : null

  const headers = new Headers(options.headers || {})
  headers.set('Content-Type', 'application/json')
  headers.set('Accept', 'application/json')

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  // Idempotency-Key support for financial and create operations
  if (options.idempotencyKey) {
    headers.set('Idempotency-Key', options.idempotencyKey)
  }

  const url = endpoint.startsWith('http') ? endpoint : `${API_BASE_URL}${endpoint}`

  const response = await fetch(url, {
    ...options,
    headers,
  })

  if (!response.ok) {
    let errorData: any
    try {
      errorData = await response.json()
    } catch {
      errorData = { detail: response.statusText }
    }

    throw new ProblemDetailsError(
      response.status,
      errorData?.detail || errorData?.title || `Lỗi yêu cầu HTTP ${response.status}`,
      errorData,
    )
  }

  if (response.status === 204) {
    return {} as T
  }

  return response.json()
}
