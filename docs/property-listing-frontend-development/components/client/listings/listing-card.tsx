'use client'

import Image from 'next/image'
import Link from 'next/link'
import { Bath, BedDouble, Heart, MapPin, Star } from 'lucide-react'
import type { Property } from '@/lib/data'
import { formatPrice } from '@/lib/format'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

type Props = {
  property: Property
  layout: 'grid' | 'list'
  highlighted?: boolean
  onHover?: (id: string | null) => void
}

export function ListingCard({ property: p, layout, highlighted, onHover }: Props) {
  const { favorites, toggleFavorite } = useStore()
  const saved = favorites.has(p.id)

  return (
    <article
      onMouseEnter={() => onHover?.(p.id)}
      onMouseLeave={() => onHover?.(null)}
      className={cn(
        'group relative overflow-hidden rounded-xl border bg-card transition-shadow hover:shadow-lg',
        highlighted ? 'border-primary shadow-lg' : 'border-border',
        layout === 'list' && 'flex',
      )}
    >
      <div className={cn('relative overflow-hidden', layout === 'grid' ? 'aspect-[16/9]' : 'w-2/5 shrink-0')}>
        <Image
          src={p.images[0]}
          alt={p.title}
          fill
          sizes={layout === 'grid' ? '(min-width: 1024px) 400px, 100vw' : '240px'}
          className="object-cover transition-transform duration-500 group-hover:scale-105"
        />
        <span className="absolute left-3 top-3 rounded-full bg-card/90 px-2.5 py-0.5 text-xs font-medium">
          {p.listingType === 'rent' ? 'Cho thuê' : 'Cần bán'}
        </span>
        {p.packageCode === 'VIP' && (
          <span className="absolute right-3 top-3 rounded bg-amber-500 text-white px-2 py-0.5 text-[10px] font-bold shadow-xs">
            VIP Nổi Bật
          </span>
        )}
      </div>
      <div className="flex min-w-0 flex-1 flex-col p-4">
        <div className="flex items-start justify-between gap-3">
          <h3 className="line-clamp-2 leading-snug">
            <Link href={`/properties/${p.id}`} className="after:absolute after:inset-0 hover:text-primary">
              {p.title}
            </Link>
          </h3>
          <button
            type="button"
            onClick={() => toggleFavorite(p.id)}
            aria-pressed={saved}
            aria-label={saved ? 'Remove from saved' : 'Save property'}
            className="relative z-10 shrink-0 text-muted-foreground transition-colors hover:text-primary"
          >
            <Heart className={cn('size-5', saved && 'fill-primary text-primary')} />
          </button>
        </div>
        <p className="text-xs text-muted-foreground mt-1 line-clamp-1">
          {[p.address, p.ward, p.district].filter(Boolean).join(', ')}
        </p>
        <div className="mt-3 flex items-center justify-between gap-2">
          <p className="font-display text-2xl font-bold text-foreground">{formatPrice(p)}</p>
          <p className="flex items-center gap-1 text-sm">
            <Star className="size-4 fill-warning text-warning" aria-hidden="true" />
            {p.rating}
            <span className="text-muted-foreground">({p.reviews})</span>
          </p>
        </div>
        <div className="mt-auto flex items-center justify-between gap-2 pt-4 text-xs text-muted-foreground border-t border-border/60">
          <span className="flex items-center gap-1.5 truncate text-primary font-medium">
            <MapPin className="size-3.5 shrink-0" aria-hidden="true" />
            {p.district || p.suburb}, {p.city || p.council}
          </span>
          <span className="flex shrink-0 items-center gap-3">
            <span className="flex items-center gap-1">
              <BedDouble className="size-3.5" aria-hidden="true" />
              {p.beds} PN
            </span>
            <span className="flex items-center gap-1">
              <Bath className="size-3.5" aria-hidden="true" />
              {p.baths} WC
            </span>
          </span>
        </div>
      </div>
    </article>
  )
}
