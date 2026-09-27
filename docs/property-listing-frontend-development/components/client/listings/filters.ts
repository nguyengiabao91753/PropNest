import type { Property, PropertyType } from '@/lib/data'

export type SortKey = 'newest' | 'price-asc' | 'price-desc' | 'rating'

export type Filters = {
  q: string
  type: 'all' | 'sale' | 'rent'
  price: [number, number]
  beds: number
  baths: number
  homeTypes: PropertyType[]
  facilities: string[]
  savedOnly: boolean
  sort: SortKey
}

// Limits in VND: Rent up to 100 million VND/mo, Sale up to 100 billion VND
export const priceLimit = (type: Filters['type']) => (type === 'rent' ? 100_000_000 : 100_000_000_000)
export const priceStep = (type: Filters['type']) => (type === 'rent' ? 2_000_000 : 1_000_000_000)

export function defaultFilters(init: Partial<Filters> = {}): Filters {
  const type = init.type ?? 'all'
  return {
    q: '',
    type,
    price: [0, priceLimit(type)],
    beds: 0,
    baths: 0,
    homeTypes: [],
    facilities: [],
    savedOnly: false,
    sort: 'newest',
    ...init,
  }
}

export function applyFilters(list: Property[], f: Filters, favorites: Set<string>) {
  const q = f.q.trim().toLowerCase()
  const limit = priceLimit(f.type)
  const result = list.filter((p) => {
    // Only published listings appear in public client search
    if (p.status !== 'Published' && p.status !== 'approved') return false

    if (f.type !== 'all' && p.listingType !== f.type) return false
    if (
      q &&
      ![p.title, p.address, p.ward, p.district, p.city, p.suburb, p.council, p.sellerName, p.id]
        .filter(Boolean)
        .some((s) => s.toLowerCase().includes(q))
    )
      return false

    if (f.type !== 'all') {
      if (p.price < f.price[0]) return false
      if (f.price[1] < limit && p.price > f.price[1]) return false
    }
    if (p.beds < f.beds || p.baths < f.baths) return false
    if (f.homeTypes.length && !f.homeTypes.includes(p.propertyType)) return false
    if (f.facilities.length && !f.facilities.every((x) => p.facilities.includes(x))) return false
    if (f.savedOnly && !favorites.has(p.id)) return false
    return true
  })

  const sorters: Record<SortKey, (a: Property, b: Property) => number> = {
    newest: (a, b) => b.createdAt.localeCompare(a.createdAt),
    'price-asc': (a, b) => a.price - b.price,
    'price-desc': (a, b) => b.price - a.price,
    rating: (a, b) => b.rating - a.rating,
  }
  return result.sort(sorters[f.sort])
}

export function activeCount(f: Filters) {
  const limit = priceLimit(f.type)
  return [
    f.type !== 'all',
    f.price[0] > 0 || f.price[1] < limit,
    f.beds > 0 || f.baths > 0,
    f.homeTypes.length > 0,
    f.facilities.length > 0 || f.savedOnly,
  ].filter(Boolean).length
}
