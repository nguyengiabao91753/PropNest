import type { Property } from './data'

const vndCurrency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export function formatPrice(p: Pick<Property, 'price' | 'listingType'>) {
  if (p.listingType === 'rent') {
    if (p.price >= 1_000_000) {
      const millions = p.price / 1_000_000
      return `${millions % 1 === 0 ? millions : millions.toFixed(1)} triệu/tháng`
    }
    return `${vndCurrency.format(p.price)}/tháng`
  }

  // Sale
  if (p.price >= 1_000_000_000) {
    const billions = p.price / 1_000_000_000
    return `${billions % 1 === 0 ? billions : billions.toFixed(1)} tỷ`
  }
  if (p.price >= 1_000_000) {
    const millions = p.price / 1_000_000
    return `${millions % 1 === 0 ? millions : millions.toFixed(1)} triệu`
  }
  return vndCurrency.format(p.price)
}

export function formatCompactPrice(p: Pick<Property, 'price' | 'listingType'>) {
  if (p.listingType === 'rent') {
    if (p.price >= 1_000_000) {
      return `${(p.price / 1_000_000).toFixed(1)} tr/th`
    }
    return `${Math.round(p.price / 1_000)}k/th`
  }

  if (p.price >= 1_000_000_000) {
    return `${(p.price / 1_000_000_000).toFixed(1)} tỷ`
  }
  if (p.price >= 1_000_000) {
    return `${Math.round(p.price / 1_000_000)} tr`
  }
  return `${p.price.toLocaleString('vi-VN')} ₫`
}

export function formatVND(amount: number) {
  return vndCurrency.format(amount)
}

export function formatNumber(n: number) {
  return n.toLocaleString('vi-VN')
}

export function formatArea(m2: number) {
  return `${m2.toLocaleString('vi-VN')} m²`
}

export function formatDate(iso: string) {
  try {
    const d = new Date(iso)
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
  } catch {
    return iso.slice(0, 10)
  }
}

export function completion(p: Property) {
  const checks = [
    p.title,
    p.address,
    p.district || p.suburb,
    p.city || p.council,
    p.description && p.description.length > 40,
    p.images && p.images.length >= 2,
    p.images && p.images.length >= 4,
    p.facilities && p.facilities.length > 0 && !p.facilities.includes('None'),
    p.area > 0 || p.landSize > 0,
    p.condition,
  ]
  return Math.round((checks.filter(Boolean).length / checks.length) * 100)
}
