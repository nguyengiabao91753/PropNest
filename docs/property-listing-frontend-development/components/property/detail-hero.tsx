'use client'

import Image from 'next/image'
import { useState } from 'react'
import { ArrowDown, ArrowUpRight, QrCode, Star } from 'lucide-react'
import { toast } from 'sonner'
import type { Property } from '@/lib/data'
import { formatCompactPrice, formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

function splitTitle(title: string) {
  const words = title.split(' ')
  if (words.length < 2) return [title, '']
  return [words.slice(0, -1).join(' '), words.at(-1) ?? '']
}

export function DetailHero({ property: p, ctaLabel, onCta }: { property: Property; ctaLabel: string; onCta: () => void }) {
  const [active, setActive] = useState(0)
  const [lead, accent] = splitTitle(p.title)
  const imgs = p.images

  async function share() {
    try {
      await navigator.clipboard.writeText(window.location.href)
      toast.success('Link copied', { description: 'Share this listing with anyone.' })
    } catch {
      toast.error('Could not copy the link')
    }
  }

  return (
    <section className="relative border-b border-border">
      <div className="mx-auto grid max-w-7xl border-x border-border md:grid-cols-2">
        <div className="flex flex-col gap-8 border-b border-border px-6 py-10 md:px-14 md:py-14">
          <div>
            <p className="text-sm text-primary">
              {p.propertyType} · {p.listingType === 'rent' ? 'For rent' : 'For sale'}
            </p>
            <h1 className="mt-3 font-display text-5xl leading-[1.05] text-balance md:text-7xl">
              {lead}
              <br />
              <em className="font-medium">{accent}</em>
            </h1>
          </div>
          <button
            type="button"
            onClick={onCta}
            className="inline-flex w-fit items-center gap-2 rounded-full bg-primary px-6 py-3 text-sm text-primary-foreground shadow-lg shadow-primary/30 transition-colors hover:bg-primary/90"
          >
            {ctaLabel}
            <ArrowUpRight className="size-4" aria-hidden="true" />
          </button>
        </div>

        <div className="flex flex-col items-end gap-8 border-b border-border px-6 py-10 text-right md:border-l md:px-14 md:py-14">
          <div className="flex items-center gap-3">
            <div>
              <div className="flex justify-end gap-0.5" aria-label={`Rated ${p.rating} out of 5`}>
                {Array.from({ length: 5 }, (_, i) => (
                  <Star
                    key={i}
                    className={cn('size-5', i < Math.round(p.rating) ? 'fill-primary text-primary' : 'text-border')}
                    aria-hidden="true"
                  />
                ))}
              </div>
              <p className="mt-1 text-sm">{p.sellerName}</p>
            </div>
            <div className="relative size-14 overflow-hidden rounded-full border border-border">
              <Image src={p.sellerAvatar} alt="" fill sizes="56px" className="object-cover" />
            </div>
          </div>
          <p className="max-w-xs text-sm leading-relaxed text-muted-foreground">
            {p.displayNotes && p.notes ? `“${p.notes}”` : `“${p.description.slice(0, 110)}…”`}
          </p>
          <dl className="flex gap-10">
            <div>
              <dd className="font-display text-4xl font-bold">{formatCompactPrice(p)}</dd>
              <dt className="mt-1 text-sm text-muted-foreground">{p.listingType === 'rent' ? 'Giá thuê / tháng' : 'Giá niêm yết'}</dt>
            </div>
            <div>
              <dd className="font-display text-4xl font-bold">
                {(p.area || p.sqft).toLocaleString('vi-VN')}
                <span className="text-primary text-2xl ml-1">m²</span>
              </dd>
              <dt className="mt-1 text-sm text-muted-foreground">Diện tích sử dụng</dt>
            </div>
          </dl>
        </div>

        <div className="flex flex-col justify-end gap-8 px-6 py-10 md:px-14 md:py-14">
          <p className="max-w-xs text-sm leading-relaxed text-muted-foreground">
            {p.showAddress
              ? [p.address, p.ward, p.district, p.city].filter(Boolean).join(', ')
              : [p.ward, p.district, p.city].filter(Boolean).join(', ')} — {formatPrice(p)}
          </p>
          <div className="flex -space-x-3" role="group" aria-label="Photos">
            {imgs.map((src, i) => (
              <button
                key={src + i}
                type="button"
                onClick={() => setActive(i)}
                aria-label={`Show photo ${i + 1}`}
                aria-pressed={active === i}
                className={cn(
                  'relative size-14 overflow-hidden rounded-full border-2 transition-transform hover:z-10 hover:scale-110',
                  active === i ? 'z-10 border-primary' : 'border-background',
                )}
              >
                <Image src={src} alt="" fill sizes="56px" className="object-cover" />
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-end justify-end gap-6 px-6 py-10 md:border-l md:border-border md:px-14 md:py-14">
          <a href="#details" aria-label="Scroll to details" className="p-2 hover:text-primary">
            <ArrowDown className="size-6" />
          </a>
          <button
            type="button"
            onClick={share}
            aria-label="Copy link to share"
            className="grid size-24 place-items-center rounded-full border border-border p-2 transition-transform hover:scale-105"
          >
            <span className="grid size-full place-items-center rounded-full bg-foreground text-background">
              <QrCode className="size-9" />
            </span>
          </button>
        </div>
      </div>

      <div className="pointer-events-none absolute inset-x-0 top-1/2 hidden -translate-y-1/2 justify-center md:flex">
        <div className="pointer-events-auto relative h-[34rem] w-[25rem]">
          <div className="relative size-full overflow-hidden rounded-full">
            <Image
              key={imgs[active]}
              src={imgs[active]}
              alt={`${p.title} photo ${active + 1}`}
              fill
              priority
              sizes="400px"
              className="animate-in fade-in object-cover duration-500"
            />
          </div>
          <button
            type="button"
            onClick={() => setActive((a) => (a + 1) % imgs.length)}
            aria-label="Next photo"
            className="absolute -right-12 bottom-24 grid size-24 place-items-center rounded-full bg-primary text-primary-foreground shadow-xl transition-transform hover:rotate-45"
          >
            <ArrowUpRight className="size-9" />
          </button>
        </div>
      </div>

      <div className="relative mx-6 -mt-2 mb-8 aspect-[4/5] overflow-hidden rounded-[3rem] md:hidden">
        <Image src={imgs[active]} alt={`${p.title} photo ${active + 1}`} fill sizes="100vw" className="object-cover" />
      </div>
    </section>
  )
}
