'use client'

import Link from 'next/link'
import { useState } from 'react'
import { SectionTitle } from '@/components/shared/brand'
import { ProjectCard } from '@/components/client/project-card'
import type { Category } from '@/lib/data'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

const tabs: { key: Category; label: string }[] = [
  { key: 'Residential', label: 'Khu dân cư' },
  { key: 'Commercial', label: 'Mặt bằng kinh doanh' },
  { key: 'Apartments', label: 'Căn hộ chung cư' },
]

export function LatestProjects() {
  const { properties } = useStore()
  const [tab, setTab] = useState<Category>('Residential')
  const items = properties
    .filter((p) => (p.status === 'Published' || (p.status as any) === 'approved') && p.category === tab)
    .slice(0, 6)

  return (
    <section className="mx-auto max-w-7xl px-4 pb-20 md:px-8 md:pb-28">
      <SectionTitle lead="Dự án Bất động sản" accent="Nổi bật" />
      <div role="tablist" aria-label="Project category" className="mt-6 flex justify-center gap-2">
        {tabs.map((t) => (
          <button
            key={t.key}
            role="tab"
            type="button"
            aria-selected={tab === t.key}
            onClick={() => setTab(t.key)}
            className={cn(
              'rounded-full border px-4 py-1.5 text-xs font-medium transition-colors',
              tab === t.key
                ? 'border-primary bg-primary text-primary-foreground font-semibold shadow-xs'
                : 'border-border bg-card hover:text-primary',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>
      {items.length === 0 ? (
        <p className="mt-10 text-center text-sm text-muted-foreground">Hiện chưa có dự án nào trong mục này.</p>
      ) : (
        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((p) => (
            <ProjectCard key={p.id} property={p} />
          ))}
        </div>
      )}
      <div className="mt-10 flex justify-center">
        <Link
          href="/listings"
          className="rounded-full border border-primary px-6 py-2.5 text-xs font-semibold text-primary transition-colors hover:bg-primary hover:text-primary-foreground shadow-xs"
        >
          Khám phá tất cả bất động sản
        </Link>
      </div>
    </section>
  )
}
