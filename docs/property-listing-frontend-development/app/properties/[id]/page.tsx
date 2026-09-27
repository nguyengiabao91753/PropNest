import { SiteHeader } from '@/components/client/site-header'
import { SiteFooter } from '@/components/client/site-footer'
import { PropertyDetail } from '@/components/property/property-detail'

export default async function PropertyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return (
    <>
      <SiteHeader />
      <main>
        <PropertyDetail id={id} mode="client" backHref="/listings" />
      </main>
      <SiteFooter />
    </>
  )
}
