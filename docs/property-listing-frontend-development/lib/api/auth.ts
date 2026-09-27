import { apiFetch } from './client'

export type RegisterRequest = {
  email: string
  password: string
  fullName: string
  phoneNumber?: string
  role: 'Seller' | 'Moderator' | 'Admin'
}

export type LoginRequest = {
  email: string
  password: string
}

export type AuthResponse = {
  token: string
  refreshToken: string
  expiresAt: string
  user: {
    id: string
    email: string
    fullName: string
    role: string
  }
}

export const authApi = {
  register: (data: RegisterRequest) =>
    apiFetch<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  login: async (data: LoginRequest) => {
    const res = await apiFetch<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    })
    if (typeof window !== 'undefined' && res.token) {
      localStorage.setItem('propnest_access_token', res.token)
      localStorage.setItem('propnest_user', JSON.stringify(res.user))
    }
    return res
  },

  logout: () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('propnest_access_token')
      localStorage.removeItem('propnest_user')
    }
  },

  getCurrentUser: () => {
    if (typeof window !== 'undefined') {
      const u = localStorage.getItem('propnest_user')
      return u ? JSON.parse(u) : null
    }
    return null
  },
}
