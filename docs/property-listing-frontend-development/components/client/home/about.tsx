import Image from 'next/image'
import Link from 'next/link'
import { ArrowUpRight } from 'lucide-react'
import { Sparkle } from '@/components/shared/brand'

const stats = [
  { value: '12K+', label: 'Satisfied customer' },
  { value: '20+', label: 'Years of experience' },
  { value: '600+', label: 'Partner companies' },
  { value: '8K+', label: 'Verified listings' },
]

function RotatingBadge() {
  return (
    <Link
      href="/#contact"
      className="group relative grid size-24 shrink-0 place-items-center rounded-full bg-background text-foreground"
      aria-label="Contact us"
    >
      <svg viewBox="0 0 100 100" className="absolute inset-0 size-full animate-[spin_14s_linear_infinite]" aria-hidden="true">
        <defs>
          <path id="badge-circle" d="M50,50 m-36,0 a36,36 0 1,1 72,0 a36,36 0 1,1 -72,0" />
        </defs>
        <text fontSize="11" letterSpacing="3" fill="currentColor">
          <textPath href="#badge-circle">CONTACT US • CONTACT US •</textPath>
        </text>
      </svg>
      <span className="grid size-10 place-items-center rounded-full bg-primary text-primary-foreground transition-transform group-hover:rotate-45">
        <ArrowUpRight className="size-5" />
      </span>
    </Link>
  )
}

export function HomeAbout() {
  return (
    <section id="about" className="mx-auto max-w-7xl scroll-mt-24 px-4 py-20 md:px-8 md:py-28">
      <div className="grid gap-8 md:grid-cols-2 md:items-end">
        <div className="flex items-center gap-8">
          <h2 className="font-display text-4xl leading-tight md:text-5xl">
            We search <br />
            the <em className="font-medium">Best</em> one
          </h2>
          <Sparkle className="size-8" />
        </div>
        <p className="max-w-md text-sm leading-relaxed text-muted-foreground md:justify-self-end">
          We connect you with premium properties thoughtfully tailored to perfectly match your unique lifestyle,
          preferences, aspirations, desires, expectations, and specific individual needs.
        </p>
      </div>

      <div className="mt-12 grid gap-6 lg:grid-cols-2">
        <dl className="grid grid-cols-2 gap-4">
          {stats.map((s) => (
            <div key={s.label} className="flex flex-col justify-between rounded-2xl border border-border bg-surface p-6">
              <dt className="order-2 mt-2 text-xs text-muted-foreground">{s.label}</dt>
              <dd className="order-1 font-display text-4xl font-semibold">{s.value}</dd>
            </div>
          ))}
        </dl>
        <div className="flex flex-col gap-6 overflow-hidden rounded-2xl bg-primary p-6 text-primary-foreground sm:flex-row sm:items-stretch">
          <div className="flex flex-1 flex-col justify-between gap-6">
            <p className="max-w-xs text-sm leading-relaxed">
              Discover your dream home with Skyline — where luxury meets comfort, and every property is a step toward
              your perfect future.
            </p>
            <RotatingBadge />
          </div>
          <div className="relative min-h-48 flex-1 overflow-hidden rounded-xl">
            <Image src="/images/p5.png" alt="White villa with a private swimming pool" fill sizes="(min-width: 1024px) 300px, 100vw" className="object-cover" />
          </div>
        </div>
      </div>
    </section>
  )
}
