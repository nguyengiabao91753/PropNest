'use client'

import Image from 'next/image'
import Link from 'next/link'
import { ArrowUpRight, Heart } from 'lucide-react'
import type { Property } from '@/lib/data'
import { formatCompactPrice, formatPrice } from '@/lib/format'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

export function ProjectCard({ property }: { property: Property }) {
  const { favorites, toggleFavorite } = useStore()
  const saved = favorites.has(property.id)

  return (
    <article className="group rounded-2xl border border-border bg-card p-3 transition-shadow hover:shadow-lg">
      <div className="relative aspect-[4/3] overflow-hidden rounded-xl">
        <Image
          src={property.images[0] || '/images/p1.png'}
          alt={property.title}
          fill
          sizes="(min-width: 1024px) 380px, (min-width: 640px) 50vw, 100vw"
          className="object-cover transition-transform duration-500 group-hover:scale-105"
        />
        {(property.packageCode === 'VIP' || property.featured) && (
          <span className="absolute left-3 top-3 rounded-full bg-amber-500 text-white font-bold px-2.5 py-0.5 text-[10px] shadow-xs">
            {property.packageCode === 'VIP' ? 'VIP Nổi Bật' : 'Nổi bật'}
          </span>
        )}
        <button
          type="button"
          onClick={() => toggleFavorite(property.id)}
          aria-pressed={saved}
          aria-label={saved ? 'Bỏ lưu BĐS' : 'Lưu tin BĐS'}
          className="absolute right-3 top-3 grid size-8 place-items-center rounded-full bg-card/90 backdrop-blur-xs transition-colors hover:text-primary"
        >
          <Heart className={cn('size-4', saved && 'fill-primary text-primary')} />
        </button>
      </div>
      <div className="mt-4 flex items-start justify-between gap-3 px-1">
        <div className="min-w-0">
          <h3 className="truncate text-sm font-semibold">
            {property.title}
            {property.beds > 0 && ` - ${property.beds} PN`}
          </h3>
          <p className="mt-1 truncate text-xs text-muted-foreground">
            {[property.district || property.suburb, property.city || property.council].filter(Boolean).join(', ')}
          </p>
        </div>
        <p className="shrink-0 font-bold text-primary text-sm">{formatCompactPrice(property)}</p>
      </div>
      <Link
        href={`/properties/${property.id}`}
        className="mx-1 mt-4 mb-1 inline-flex items-center gap-1 rounded-full border border-border px-3.5 py-1 text-xs font-medium transition-colors hover:border-primary hover:text-primary"
      >
        Xem chi tiết
        <ArrowUpRight className="size-3.5" aria-hidden="true" />
      </Link>
    </article>
  )
}
