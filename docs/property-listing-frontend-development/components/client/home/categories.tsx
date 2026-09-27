import Image from 'next/image'
import Link from 'next/link'
import { Building, Building2, Hotel, Landmark, Sofa, TreePine } from 'lucide-react'
import { Sparkle } from '@/components/shared/brand'

const cats = [
  { label: 'Interior design', Icon: Sofa, href: '/listings?home=Apartment' },
  { label: 'Commercial architect', Icon: Building2, href: '/listings?home=Commercial' },
  { label: 'Landscape architect', Icon: TreePine, href: '/listings?home=Villa' },
  { label: 'Civic project', Icon: Landmark, href: '/listings' },
  { label: 'Urban Development', Icon: Building, href: '/listings?home=Townhouse' },
  { label: 'Hospitality Ventures', Icon: Hotel, href: '/listings?home=House' },
]

export function ProjectCategories() {
  return (
    <section className="relative isolate overflow-hidden bg-foreground py-20 text-background md:py-28">
      <Image src="/images/categories-bg.png" alt="" fill sizes="100vw" className="-z-10 object-cover opacity-60" />
      <div className="absolute inset-0 -z-10 bg-foreground/50" aria-hidden="true" />
      <div className="mx-auto max-w-5xl px-4 text-center md:px-8">
        <div className="flex items-center justify-center gap-6">
          <Sparkle className="size-6" />
          <h2 className="font-display text-3xl md:text-5xl">
            Project <em className="font-medium">Categories</em>
          </h2>
          <Sparkle className="size-6" />
        </div>
        <p className="mx-auto mt-3 max-w-sm text-sm text-background/80">
          Discover various real estate project categories, from residential to commercial and industrial.
        </p>
        <ul className="mt-12 grid grid-cols-1 gap-y-8 sm:grid-cols-2 md:grid-cols-3">
          {cats.map(({ label, Icon, href }, i) => (
            <li key={label} className={i % 3 !== 0 ? 'md:border-l md:border-background/20' : ''}>
              <Link href={href} className="group inline-flex items-center gap-3 px-4 py-2 text-sm">
                <span className="grid size-8 place-items-center rounded-full bg-primary transition-transform group-hover:scale-110">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                <span className="group-hover:text-primary">{label}</span>
              </Link>
            </li>
          ))}
        </ul>
        <Link
          href="/listings"
          className="mt-12 inline-flex rounded-full bg-primary px-5 py-2 text-sm text-primary-foreground transition-colors hover:bg-primary/90"
        >
          Our work
        </Link>
      </div>
    </section>
  )
}
