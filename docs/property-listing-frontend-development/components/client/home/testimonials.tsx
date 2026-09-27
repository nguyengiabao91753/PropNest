'use client'

import Image from 'next/image'
import { useState } from 'react'
import { ArrowLeft, ArrowRight, Quote } from 'lucide-react'
import { testimonials } from '@/lib/data'

const PER_PAGE = 3

export function Testimonials() {
  const [start, setStart] = useState(0)
  const visible = Array.from({ length: PER_PAGE }, (_, i) => testimonials[(start + i) % testimonials.length])

  return (
    <section className="bg-surface py-20 md:py-28">
      <div className="mx-auto max-w-6xl px-4 md:px-8">
        <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
          <h2 className="font-display text-4xl md:text-5xl">
            Our <em className="font-medium">Testimonials</em>
          </h2>
          <p className="max-w-xs text-sm text-muted-foreground">
            See how we&apos;ve turned clients&apos; real estate dreams into reality with exceptional service
          </p>
        </div>
        <ul className="mt-10 grid gap-4 md:grid-cols-3" aria-live="polite">
          {visible.map((t) => (
            <li key={t.name} className="rounded-2xl border border-border bg-card p-5">
              <div className="flex items-start gap-3">
                <div className="relative size-10 overflow-hidden rounded-full">
                  <Image src={t.avatar} alt="" fill sizes="40px" className="object-cover" />
                </div>
                <div className="flex-1">
                  <p className="text-sm font-semibold">{t.name}</p>
                  <p className="text-xs text-muted-foreground">{t.date}</p>
                </div>
                <Quote className="size-6 text-border" aria-hidden="true" />
              </div>
              <p className="mt-4 text-sm leading-relaxed text-muted-foreground">{t.text}</p>
            </li>
          ))}
        </ul>
        <div className="mt-8 flex justify-center gap-3">
          <button
            type="button"
            aria-label="Previous testimonials"
            onClick={() => setStart((s) => (s - 1 + testimonials.length) % testimonials.length)}
            className="grid size-9 place-items-center rounded-full border border-border transition-colors hover:border-primary hover:text-primary"
          >
            <ArrowLeft className="size-4" />
          </button>
          <button
            type="button"
            aria-label="Next testimonials"
            onClick={() => setStart((s) => (s + 1) % testimonials.length)}
            className="grid size-9 place-items-center rounded-full border border-border transition-colors hover:border-primary hover:text-primary"
          >
            <ArrowRight className="size-4" />
          </button>
        </div>
      </div>
    </section>
  )
}
