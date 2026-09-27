import { PropertyDetail } from '@/components/property/property-detail'

export default async function SellerPropertyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <PropertyDetail id={id} mode="seller" backHref="/seller/properties" />
}
