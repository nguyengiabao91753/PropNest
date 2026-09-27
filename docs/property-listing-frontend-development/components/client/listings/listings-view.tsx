'use client'

import dynamic from 'next/dynamic'
import { useDeferredValue, useMemo, useState } from 'react'
import { LayoutGrid, List, Map as MapIcon, SearchX } from 'lucide-react'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'
import { FilterBar } from './filter-bar'
import { ListingCard } from './listing-card'
import { applyFilters, defaultFilters, type Filters } from './filters'

const ListingsMap = dynamic(() => import('./listings-map'), {
  ssr: false,
  loading: () => <div className="size-full animate-pulse rounded-2xl bg-muted" />,
})

const PAGE = 8

export function ListingsView({ initial }: { initial: Partial<Filters> }) {
  const { properties, favorites } = useStore()
  const [filters, setFilters] = useState(() => defaultFilters(initial))
  const [layout, setLayout] = useState<'grid' | 'list'>('grid')
  const [hovered, setHovered] = useState<string | null>(null)
  const [visible, setVisible] = useState(PAGE)
  const [showMapMobile, setShowMapMobile] = useState(false)

  const deferred = useDeferredValue(filters)
  const results = useMemo(() => applyFilters(properties, deferred, favorites), [properties, deferred, favorites])
  const shown = results.slice(0, visible)

  function update(f: Filters) {
    setFilters(f)
    setVisible(PAGE)
  }

  return (
    <div className="mx-auto max-w-[1400px] px-4 py-5 md:px-8">
      <FilterBar filters={filters} onChange={update} />

      <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,0.9fr)]">
        <section aria-labelledby="results-title">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 id="results-title" className="font-display text-3xl font-bold md:text-4xl text-foreground">
                {results.length} bất động sản đang mở bán & cho thuê
              </h1>
              <p className="mt-1 text-xs text-muted-foreground">
                Khám phá danh sách nhà đất và căn hộ đã được kiểm duyệt minh bạch tại Việt Nam.
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <button
                type="button"
                onClick={() => setShowMapMobile((v) => !v)}
                aria-pressed={showMapMobile}
                className="grid size-10 place-items-center rounded-lg border border-border bg-card lg:hidden"
                aria-label="Bật tắt bản đồ"
              >
                <MapIcon className="size-4" />
              </button>
              <div className="flex overflow-hidden rounded-lg border border-border bg-card" role="group" aria-label="Bố cục hiển thị">
                {(['grid', 'list'] as const).map((l) => (
                  <button
                    key={l}
                    type="button"
                    aria-pressed={layout === l}
                    aria-label={`Hiển thị dạng ${l === 'list' ? 'danh sách' : 'lưới'}`}
                    onClick={() => setLayout(l)}
                    className={cn(
                      'grid size-10 place-items-center transition-colors',
                      layout === l ? 'bg-primary text-primary-foreground' : 'hover:text-primary',
                    )}
                  >
                    {l === 'list' ? <List className="size-4" /> : <LayoutGrid className="size-4" />}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {showMapMobile && (
            <div className="mt-5 h-80 lg:hidden">
              <ListingsMap properties={results} activeId={hovered} />
            </div>
          )}

          {results.length === 0 ? (
            <div className="mt-10 flex flex-col items-center rounded-2xl border border-dashed border-border py-16 text-center">
              <SearchX className="size-10 text-muted-foreground" aria-hidden="true" />
              <p className="mt-4 font-semibold text-foreground text-sm">Không tìm thấy bất động sản nào phù hợp</p>
              <p className="text-xs text-muted-foreground mt-1">Hãy thử nới rộng khoảng giá hoặc chọn khu vực khác.</p>
              <button
                type="button"
                onClick={() => update(defaultFilters())}
                className="mt-4 rounded-full border border-primary px-4 py-1.5 text-xs font-semibold text-primary hover:bg-primary hover:text-primary-foreground transition-colors"
              >
                Xóa tất cả bộ lọc
              </button>
            </div>
          ) : (
            <>
              <div className={cn('mt-6 grid gap-4', layout === 'grid' ? 'sm:grid-cols-2' : 'grid-cols-1')}>
                {shown.map((p) => (
                  <ListingCard key={p.id} property={p} layout={layout} highlighted={hovered === p.id} onHover={setHovered} />
                ))}
              </div>
              {visible < results.length && (
                <div className="mt-8 flex justify-center">
                  <button
                    type="button"
                    onClick={() => setVisible((v) => v + PAGE)}
                    className="rounded-full border border-primary px-6 py-2.5 text-xs font-semibold text-primary transition-colors hover:bg-primary hover:text-primary-foreground shadow-xs"
                  >
                    Tải thêm ({results.length - visible} tin khác)
                  </button>
                </div>
              )}
            </>
          )}
        </section>

        <aside className="sticky top-24 hidden h-[calc(100dvh-8rem)] lg:block" aria-label="Bản đồ vị trí">
          <ListingsMap properties={results} activeId={hovered} />
        </aside>
      </div>
    </div>
  )
}
