import type { Metadata } from 'next'
import { SellerPropertiesView } from '@/components/seller/properties-view'

export const metadata: Metadata = { title: 'Bất động sản của tôi - PropNest Seller' }

export default async function SellerPropertiesPage({ searchParams }: { searchParams: Promise<{ status?: string }> }) {
  const { status } = await searchParams
  const initial = status || 'all'
  return <SellerPropertiesView key={initial} initialStatus={initial} />
}
