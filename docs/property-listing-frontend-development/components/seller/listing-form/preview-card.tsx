'use client'

import Image from 'next/image'
import { useState } from 'react'
import { formatArea, formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Draft } from './fields'

export function PreviewCard({ draft }: { draft: Draft }) {
  const [active, setActive] = useState(0)
  const images = draft.images.length ? draft.images : ['/images/p2.png']
  const idx = Math.min(active, images.length - 1)
  const price = Number(draft.price) || 0
  const area = Number(draft.sqft || draft.area) || 0

  const specs = [
    draft.beds && `${draft.beds} PN`,
    draft.baths && `${draft.baths} WC`,
    area > 0 && formatArea(area),
  ].filter(Boolean)

  return (
    <article className="overflow-hidden rounded-xl border border-border bg-card shadow-sm">
      <div className="relative aspect-[3/2] overflow-hidden">
        <Image src={images[idx]} alt="Listing cover preview" fill sizes="300px" className="object-cover" />
        {draft.bannerOn && (
          <span className="absolute right-[-44px] top-6 w-44 rotate-45 bg-amber-500 py-1 text-center text-[10px] font-semibold uppercase tracking-wide text-white shadow">
            {draft.banner || 'VIP Nổi Bật'}
          </span>
        )}
        <div className="absolute inset-x-0 bottom-3 flex justify-center gap-1.5">
          {images.slice(0, 5).map((_, i) => (
            <button
              key={i}
              type="button"
              onClick={() => setActive(i)}
              aria-label={`Preview photo ${i + 1}`}
              className={cn('size-2 rounded-full', i === idx ? 'bg-primary' : 'bg-card/90')}
            />
          ))}
        </div>
      </div>
      <div className="flex flex-col gap-2 p-4">
        <p className="font-display text-2xl font-bold text-foreground">
          {price > 0 ? formatPrice({ price, listingType: draft.listingType }) : 'Chưa định giá'}
        </p>
        <p className="text-xs text-muted-foreground line-clamp-1">
          {draft.showAddress
            ? [draft.streetNumber, draft.address].filter(Boolean).join(' ') +
                (draft.ward ? `, ${draft.ward}` : '') +
                (draft.district ? `, ${draft.district}` : '') || 'Địa chỉ BĐS'
            : [draft.ward, draft.district].filter(Boolean).join(', ') || 'Địa chỉ ẩn'}
        </p>
        <div className="flex flex-wrap items-center gap-x-2 text-xs text-foreground/80">
          {specs.map((s, i) => (
            <span key={i} className="flex items-center gap-2">
              {i > 0 && <span className="size-1 rounded-full bg-border" aria-hidden="true" />}
              {s}
            </span>
          ))}
        </div>
      </div>
    </article>
  )
}
