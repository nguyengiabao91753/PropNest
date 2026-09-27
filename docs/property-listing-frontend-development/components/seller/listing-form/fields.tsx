'use client'

import { Check, ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { PackageCode, Property } from '@/lib/data'

export type Draft = Pick<
  Property,
  | 'address'
  | 'streetNumber'
  | 'city'
  | 'district'
  | 'ward'
  | 'suburb'
  | 'council'
  | 'propertyType'
  | 'condition'
  | 'facilities'
  | 'notes'
  | 'displayNotes'
  | 'title'
  | 'description'
  | 'listingType'
  | 'images'
  | 'showAddress'
> & {
  beds: string
  baths: string
  carports: string
  landSize: string
  area: string
  sqft: string
  price: string
  bannerOn: boolean
  banner: string
  packageCode: PackageCode
}

export type Errors = Partial<Record<keyof Draft | 'confirm', string>>

export const emptyDraft: Draft = {
  address: '',
  streetNumber: '',
  city: 'TP. Hồ Chí Minh',
  district: 'Quận 2 (Thủ Đức)',
  ward: 'Thảo Điền',
  suburb: 'Quận 2 (Thủ Đức)',
  council: 'TP. Hồ Chí Minh',
  propertyType: 'House',
  beds: '3',
  baths: '2',
  carports: '1',
  area: '120',
  landSize: '150',
  sqft: '120',
  condition: 'Nhà mới đẹp',
  facilities: ['Bảo vệ 24/7'],
  notes: '',
  displayNotes: false,
  title: '',
  description: '',
  listingType: 'sale',
  price: '8500000000',
  images: [],
  bannerOn: true,
  banner: 'Hot Listing',
  packageCode: 'VIP',
  showAddress: true,
}

export const inputCls =
  'h-10 w-full rounded-lg border bg-card px-3 text-sm outline-none transition-colors placeholder:text-muted-foreground focus:border-foreground/60 aria-invalid:border-destructive'

export function Field({
  label,
  htmlFor,
  error,
  className,
  children,
}: {
  label: string
  htmlFor: string
  error?: string
  className?: string
  children: React.ReactNode
}) {
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={htmlFor} className="text-xs text-muted-foreground">
        {label}
      </label>
      {children}
      {error && (
        <p id={`${htmlFor}-error`} className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

export function SelectInput({
  id,
  value,
  onChange,
  placeholder,
  options,
  error,
}: {
  id: string
  value: string
  onChange: (v: string) => void
  placeholder: string
  options: readonly string[]
  error?: string
}) {
  return (
    <div className="relative">
      <select
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-invalid={!!error}
        aria-describedby={error ? `${id}-error` : undefined}
        className={cn(inputCls, 'appearance-none pr-9 border-border', !value && 'text-muted-foreground')}
      >
        <option value="" disabled>
          {placeholder}
        </option>
        {options.map((o) => (
          <option key={o} value={o} className="text-foreground">
            {o}
          </option>
        ))}
      </select>
      <ChevronDown className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2" aria-hidden="true" />
    </div>
  )
}

export function SquareCheck({ checked, onChange, label }: { checked: boolean; onChange: (c: boolean) => void; label: string }) {
  return (
    <label className="inline-flex cursor-pointer items-center gap-2 text-xs text-muted-foreground">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="peer sr-only" />
      <span
        className="grid size-4 place-items-center rounded-[3px] border border-foreground/70 bg-card transition-colors peer-checked:border-foreground peer-checked:bg-foreground peer-focus-visible:ring-2 peer-focus-visible:ring-ring"
        aria-hidden="true"
      >
        {checked && <Check className="size-3 text-background" strokeWidth={3} />}
      </span>
      {label}
    </label>
  )
}

export function Toggle({ checked, onChange, label, id }: { checked: boolean; onChange: (c: boolean) => void; label: string; id: string }) {
  return (
    <div className="flex items-center justify-between">
      <label htmlFor={id} className="text-sm">
        {label}
      </label>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={cn(
          'relative h-6 w-10 rounded-full border border-border transition-colors',
          checked ? 'bg-brand-soft' : 'bg-card',
        )}
      >
        <span
          className={cn(
            'absolute top-1/2 size-4 -translate-y-1/2 rounded-full transition-all',
            checked ? 'left-[calc(100%-1.25rem)] bg-primary' : 'left-1 bg-muted-foreground/40',
          )}
        />
      </button>
    </div>
  )
}
