import { ListingWizard } from '@/components/seller/listing-form/listing-wizard'

export default async function SellerEditPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <ListingWizard editId={id} />
}
