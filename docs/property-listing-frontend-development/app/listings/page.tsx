import type { Metadata } from 'next'
import { SiteHeader } from '@/components/client/site-header'
import { ListingsView } from '@/components/client/listings/listings-view'
import type { Filters } from '@/components/client/listings/filters'
import { PROPERTY_TYPES, type PropertyType } from '@/lib/data'

export const metadata: Metadata = {
  title: 'Search properties',
  description: 'Search and filter verified homes for sale and rent across London.',
}

export default async function ListingsPage({ searchParams }: { searchParams: Promise<Record<string, string | undefined>> }) {
  const sp = await searchParams
  const initial: Partial<Filters> = {}
  if (sp.q) initial.q = sp.q
  if (sp.type === 'sale' || sp.type === 'rent') initial.type = sp.type
  if (sp.home && PROPERTY_TYPES.includes(sp.home as PropertyType)) initial.homeTypes = [sp.home as PropertyType]
  if (sp.saved) initial.savedOnly = true

  return (
    <>
      <SiteHeader />
      <main>
        <ListingsView key={JSON.stringify(sp)} initial={initial} />
      </main>
    </>
  )
}
