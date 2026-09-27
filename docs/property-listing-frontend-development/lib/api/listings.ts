import { apiFetch } from './client'
import type { ListingHistory, PackageCode, Property } from '@/lib/data'

export type CreateListingDto = {
  title: string
  description: string
  propertyType: string
  listingType: string
  price: number
  area: number
  city: string
  district: string
  ward: string
  address: string
}

export type UpdateListingDto = Partial<CreateListingDto>

export const listingsApi = {
  // GET /listings
  getAll: (params?: { page?: number; perPage?: number; city?: string; type?: string }) => {
    const q = new URLSearchParams()
    if (params?.page) q.set('page', String(params.page))
    if (params?.perPage) q.set('perPage', String(params.perPage))
    if (params?.city) q.set('city', params.city)
    if (params?.type) q.set('type', params.type)
    return apiFetch<Property[]>(`/listings?${q.toString()}`)
  },

  // GET /listings/{id}
  getById: (id: string) => apiFetch<Property>(`/listings/${id}`),

  // POST /listings (Draft)
  createDraft: (data: CreateListingDto) =>
    apiFetch<Property>('/listings', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  // PUT /listings/{id} (With Optimistic Concurrency RowVersion)
  update: (id: string, data: UpdateListingDto, rowVersion?: string) =>
    apiFetch<Property>(`/listings/${id}`, {
      method: 'PUT',
      headers: rowVersion ? { 'If-Match': rowVersion } : {},
      body: JSON.stringify(data),
    }),

  // POST /listings/{id}/submit
  submit: (id: string) =>
    apiFetch<{ message: string }>(`/listings/${id}/submit`, {
      method: 'POST',
    }),

  // POST /listings/{id}/purchase-package (Idempotency Key required)
  purchasePackage: (id: string, packageCode: PackageCode, idempotencyKey: string) =>
    apiFetch<{ correlationId: string; status: string }>(`/listings/${id}/purchase-package`, {
      method: 'POST',
      idempotencyKey,
      body: JSON.stringify({ packageCode }),
    }),

  // GET /listings/{id}/history (History Delta Engine)
  getHistory: (id: string) => apiFetch<ListingHistory[]>(`/listings/${id}/history`),

  // Moderation endpoints
  approve: (id: string) =>
    apiFetch<{ success: boolean }>(`/moderation/listings/${id}/approve`, {
      method: 'POST',
    }),

  reject: (id: string, reason: string) =>
    apiFetch<{ success: boolean }>(`/moderation/listings/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
}
