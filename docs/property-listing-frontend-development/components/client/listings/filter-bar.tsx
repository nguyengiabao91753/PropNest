'use client'

import { ChevronDown, Search, X } from 'lucide-react'
import { toast } from 'sonner'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Slider } from '@/components/ui/slider'
import { Checkbox } from '@/components/ui/checkbox'
import { FACILITIES, PROPERTY_TYPES } from '@/lib/data'
import { cn } from '@/lib/utils'
import { activeCount, defaultFilters, priceLimit, priceStep, type Filters, type SortKey } from './filters'

type Props = { filters: Filters; onChange: (f: Filters) => void }

const money = (n: number) =>
  n >= 1_000_000_000 ? `${(n / 1_000_000_000).toFixed(1)} tỷ` : n >= 1_000_000 ? `${Math.round(n / 1_000_000)} triệu` : `${n.toLocaleString('vi-VN')} ₫`

function Trigger({ label, active }: { label: string; active?: boolean }) {
  return (
    <PopoverTrigger
      className={cn(
        'inline-flex h-11 shrink-0 items-center gap-2 rounded-lg border bg-card px-4 text-sm outline-none transition-colors hover:border-primary focus-visible:border-primary data-[popup-open]:border-primary',
        active ? 'border-primary text-primary' : 'border-border',
      )}
    >
      {label}
      <ChevronDown className="size-4" aria-hidden="true" />
    </PopoverTrigger>
  )
}

function Chip({ selected, onClick, children }: { selected: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={cn(
        'min-w-10 rounded-lg border px-3 py-1.5 text-sm transition-colors',
        selected ? 'border-primary bg-primary text-primary-foreground' : 'border-border hover:border-primary',
      )}
    >
      {children}
    </button>
  )
}

export function FilterBar({ filters: f, onChange }: Props) {
  const set = (patch: Partial<Filters>) => onChange({ ...f, ...patch })
  const limit = priceLimit(f.type)
  const typeLabel = f.type === 'rent' ? 'Cho thuê' : f.type === 'sale' ? 'Cần bán' : 'Mua hoặc Thuê'
  const count = activeCount(f)

  const toggle = <T,>(list: T[], v: T) => (list.includes(v) ? list.filter((x) => x !== v) : [...list, v])

  return (
    <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
      <label className="relative flex h-11 flex-1 items-center rounded-lg border border-border bg-card focus-within:border-primary">
        <span className="sr-only">Tìm kiếm tin bất động sản</span>
        <input
          type="search"
          value={f.q}
          onChange={(e) => set({ q: e.target.value })}
          placeholder="Tìm theo địa chỉ, quận, tên dự án, mã tin..."
          className="h-full min-w-0 flex-1 bg-transparent px-4 text-sm outline-none placeholder:text-muted-foreground"
        />
        <Search className="mr-4 size-5 text-muted-foreground" aria-hidden="true" />
      </label>

      <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 lg:mx-0 lg:overflow-visible lg:px-0 lg:pb-0">
        <Popover>
          <Trigger label={typeLabel} active={f.type !== 'all'} />
          <PopoverContent align="start" className="w-48 p-2">
            {(['all', 'sale', 'rent'] as const).map((t) => (
              <button
                key={t}
                type="button"
                onClick={() => set({ type: t, price: [0, priceLimit(t)] })}
                className={cn(
                  'w-full rounded-md px-3 py-2 text-left text-sm hover:bg-accent',
                  f.type === t && 'bg-brand-soft text-primary',
                )}
              >
                {t === 'all' ? 'Buy or Rent' : t === 'sale' ? 'For Sale' : 'For Rent'}
              </button>
            ))}
          </PopoverContent>
        </Popover>

        <Popover>
          <Trigger label="Price" active={f.price[0] > 0 || f.price[1] < limit} />
          <PopoverContent align="start" className="w-72 p-4">
            {f.type === 'all' ? (
              <p className="text-sm text-muted-foreground">Choose For Sale or For Rent first to set a price range.</p>
            ) : (
              <>
                <p className="text-sm font-medium">Price range</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {money(f.price[0])} – {f.price[1] >= limit ? `${money(limit)}+` : money(f.price[1])}
                  {f.type === 'rent' && ' / month'}
                </p>
                <Slider
                  className="mt-5"
                  min={0}
                  max={limit}
                  step={priceStep(f.type)}
                  value={f.price}
                  onValueChange={(v) => Array.isArray(v) && set({ price: [v[0], v[1]] })}
                  aria-label="Price range"
                />
              </>
            )}
          </PopoverContent>
        </Popover>

        <Popover>
          <Trigger label="Bed & Baths" active={f.beds > 0 || f.baths > 0} />
          <PopoverContent align="start" className="w-72 p-4">
            {(['beds', 'baths'] as const).map((key) => (
              <fieldset key={key} className="mb-4 last:mb-0">
                <legend className="mb-2 text-sm font-medium">{key === 'beds' ? 'Bedrooms' : 'Bathrooms'}</legend>
                <div className="flex flex-wrap gap-2">
                  {[0, 1, 2, 3, 4].map((n) => (
                    <Chip key={n} selected={f[key] === n} onClick={() => set({ [key]: n })}>
                      {n === 0 ? 'Any' : `${n}+`}
                    </Chip>
                  ))}
                </div>
              </fieldset>
            ))}
          </PopoverContent>
        </Popover>

        <Popover>
          <Trigger label="Home Type" active={f.homeTypes.length > 0} />
          <PopoverContent align="start" className="w-56 p-3">
            <div className="flex flex-col gap-2.5">
              {PROPERTY_TYPES.map((t) => (
                <label key={t} className="flex cursor-pointer items-center gap-2.5 text-sm">
                  <Checkbox checked={f.homeTypes.includes(t)} onCheckedChange={() => set({ homeTypes: toggle(f.homeTypes, t) })} />
                  {t}
                </label>
              ))}
            </div>
          </PopoverContent>
        </Popover>

        <Popover>
          <Trigger label="More" active={f.facilities.length > 0 || f.savedOnly} />
          <PopoverContent align="end" className="w-72 p-4">
            <p className="mb-2 text-sm font-medium">Facilities</p>
            <div className="grid grid-cols-2 gap-2.5">
              {FACILITIES.filter((x) => x !== 'None').map((fac) => (
                <label key={fac} className="flex cursor-pointer items-center gap-2 text-sm">
                  <Checkbox checked={f.facilities.includes(fac)} onCheckedChange={() => set({ facilities: toggle(f.facilities, fac) })} />
                  {fac}
                </label>
              ))}
            </div>
            <label className="mt-4 flex cursor-pointer items-center gap-2 border-t border-border pt-4 text-sm">
              <Checkbox checked={f.savedOnly} onCheckedChange={(c) => set({ savedOnly: Boolean(c) })} />
              Saved homes only
            </label>
            <label className="mt-4 flex flex-col gap-1.5 text-sm font-medium">
              Sort by
              <select
                value={f.sort}
                onChange={(e) => set({ sort: e.target.value as SortKey })}
                className="h-9 rounded-md border border-border bg-card px-2 text-sm font-normal outline-none focus:border-primary"
              >
                <option value="newest">Newest</option>
                <option value="price-asc">Price: low to high</option>
                <option value="price-desc">Price: high to low</option>
                <option value="rating">Top rated</option>
              </select>
            </label>
          </PopoverContent>
        </Popover>

        {count > 0 && (
          <button
            type="button"
            onClick={() => onChange(defaultFilters({ q: f.q, sort: f.sort }))}
            className="inline-flex h-11 shrink-0 items-center gap-1 rounded-lg px-3 text-sm text-muted-foreground hover:text-primary"
          >
            <X className="size-4" aria-hidden="true" />
            Clear ({count})
          </button>
        )}

        <button
          type="button"
          onClick={() => toast.success('Search saved', { description: "We'll notify you when new homes match." })}
          className="inline-flex h-11 shrink-0 items-center rounded-lg bg-primary px-5 text-sm text-primary-foreground transition-colors hover:bg-primary/90"
        >
          Save Search
        </button>
      </div>
    </div>
  )
}
