import Link from 'next/link'
import { Plus } from 'lucide-react'
import { SectionTitle } from '@/components/shared/brand'

const faqs = [
  { q: 'What services does Skyline offer?', a: 'We help you buy, rent and sell residential and commercial properties, with verified listings, virtual tours and dedicated agents.' },
  { q: 'Does Skyline verify listed properties?', a: 'Yes, all properties undergo a thorough verification process by our admin team to ensure accuracy, legality, and quality before being listed.' },
  { q: 'How can I search for properties?', a: 'Use the search bar on the home page or open the Listing page to filter by price, bedrooms, bathrooms, home type and location on an interactive map.' },
  { q: 'Does Skyline provide legal assistance?', a: 'Our partner network includes conveyancers and solicitors who can guide you through contracts and settlement.' },
]

export function FaqSection() {
  return (
    <section className="mx-auto max-w-3xl px-4 pb-20 md:px-8 md:pb-28">
      <SectionTitle lead="Frequently Asked" accent="Question" />
      <p className="mx-auto mt-3 max-w-sm text-center text-sm text-muted-foreground">
        Explore how we&apos;ve brought clients&apos; real estate visions to life with unmatched expertise and dedicated support
      </p>
      <div className="mt-10 flex flex-col gap-3">
        {faqs.map((f, i) => (
          <details key={f.q} open={i === 1} className="group rounded-xl border border-border bg-card px-5 py-4">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-sm font-medium [&::-webkit-details-marker]:hidden">
              {f.q}
              <Plus className="size-4 shrink-0 transition-transform group-open:rotate-45" aria-hidden="true" />
            </summary>
            <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{f.a}</p>
          </details>
        ))}
      </div>
      <div className="mt-8 flex justify-center">
        <Link
          href="/#contact"
          className="rounded-full border border-primary px-5 py-2 text-sm text-primary transition-colors hover:bg-primary hover:text-primary-foreground"
        >
          Ask a question
        </Link>
      </div>
    </section>
  )
}
