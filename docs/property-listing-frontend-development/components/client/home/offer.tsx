import Image from 'next/image'
import Link from 'next/link'
import { Clock, Cpu, LayoutTemplate, ShieldCheck } from 'lucide-react'
import { SectionTitle } from '@/components/shared/brand'

const left = [
  { Icon: Cpu, title: 'Luxury Architecture and Technology', text: 'High-end design and innovation seamlessly integrate to deliver high-tech living.' },
  { Icon: ShieldCheck, title: 'Years of Guarantee', text: 'Our swift execution ensures your vision becomes reality without any kind of delay.' },
]
const right = [
  { Icon: LayoutTemplate, title: 'Efficient layout design', text: 'Our designs maximize functionality and elegance, tailored for perfection.' },
  { Icon: Clock, title: 'Short Implementation time', text: 'Enjoy long-lasting quality backed by our trusted expertise and assurance.' },
]

function Feature({ Icon, title, text }: (typeof left)[number]) {
  return (
    <div className="flex gap-3">
      <span className="grid size-7 shrink-0 place-items-center rounded-full bg-brand-soft text-primary">
        <Icon className="size-3.5" aria-hidden="true" />
      </span>
      <div>
        <h3 className="text-sm font-semibold text-primary">{title}</h3>
        <p className="mt-1 max-w-56 text-xs leading-relaxed text-muted-foreground">{text}</p>
      </div>
    </div>
  )
}

export function WhatWeOffer() {
  return (
    <section className="bg-surface py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-8">
        <SectionTitle lead="What we" accent="offer" />
        <p className="mx-auto mt-3 max-w-md text-center text-sm text-muted-foreground">
          We offer the latest property listings with detailed insights, virtual tours, and personalized assistance.
        </p>
        <div className="mt-12 grid items-center gap-10 md:grid-cols-[1fr_1.2fr_1fr]">
          <div className="flex flex-col gap-16">
            {left.map((f) => (
              <Feature key={f.title} {...f} />
            ))}
          </div>
          <div className="relative mx-auto aspect-square w-full max-w-sm">
            <Image src="/images/offer-house.png" alt="Illustrated Tudor-style family home" fill sizes="384px" className="object-contain mix-blend-multiply" />
          </div>
          <div className="flex flex-col gap-16 md:items-end">
            {right.map((f) => (
              <Feature key={f.title} {...f} />
            ))}
          </div>
        </div>
        <div className="mt-10 flex justify-center">
          <Link
            href="/signup"
            className="rounded-full border border-primary px-5 py-2 text-sm text-primary transition-colors hover:bg-primary hover:text-primary-foreground"
          >
            Get started
          </Link>
        </div>
      </div>
    </section>
  )
}
