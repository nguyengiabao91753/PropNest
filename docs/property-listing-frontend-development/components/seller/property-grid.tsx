'use client'

import Image from 'next/image'
import Link from 'next/link'
import { StatusBadge } from '@/components/shared/status-badge'
import type { Property } from '@/lib/data'
import { formatArea, formatPrice } from '@/lib/format'
import { RowActions } from './property-table'

export function PropertyGrid({
  rows,
  onDelete,
  onViewHistory,
  onSubmitForModeration,
  onToggleHide,
}: {
  rows: Property[]
  onDelete: (p: Property) => void
  onViewHistory?: (p: Property) => void
  onSubmitForModeration?: (p: Property) => void
  onToggleHide?: (p: Property) => void
}) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {rows.map((p) => (
        <article key={p.id} className="overflow-hidden rounded-xl border border-border bg-card flex flex-col justify-between">
          <div>
            <div className="relative aspect-[4/3]">
              <Image src={p.images[0] || '/images/p1.png'} alt={p.title} fill sizes="(min-width: 1280px) 25vw, (min-width: 640px) 50vw, 100vw" className="object-cover" />
              <StatusBadge status={p.status} className="absolute left-3 top-3 bg-card shadow-xs" />
              {p.packageCode === 'VIP' && (
                <span className="absolute right-3 top-3 rounded bg-amber-500 text-white px-2 py-0.5 text-[10px] font-bold shadow-xs">
                  VIP
                </span>
              )}
            </div>
            <div className="p-4">
              <h3 className="truncate font-medium text-foreground">
                <Link href={`/properties/${p.id}`} className="hover:text-primary">
                  {p.title}
                </Link>
              </h3>
              <p className="text-xs text-muted-foreground mt-0.5 line-clamp-1">
                {[p.address, p.ward, p.district].filter(Boolean).join(', ')}
              </p>
              <p className="text-xs text-muted-foreground mt-1 font-mono">
                {p.id} · {formatArea(p.area || p.sqft)}
              </p>
              <p className="mt-2 font-display text-lg font-bold text-foreground">{formatPrice(p)}</p>
            </div>
          </div>

          <div className="flex items-center justify-between border-t border-border px-4 py-2.5 bg-surface/40">
            <span className="text-[11px] text-muted-foreground font-mono">Gói: {p.packageCode || 'Standard'}</span>
            <RowActions
              property={p}
              onDelete={onDelete}
              onViewHistory={onViewHistory}
              onSubmitForModeration={onSubmitForModeration}
              onToggleHide={onToggleHide}
            />
          </div>
        </article>
      ))}
    </div>
  )
}
