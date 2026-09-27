'use client'

import Image from 'next/image'
import { useRef, useState } from 'react'
import { ImagePlus, Star, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Field, inputCls, type Draft, type Errors } from './fields'

const SAMPLES = ['/images/p1.png', '/images/i1.png', '/images/i2.png', '/images/i3.png', '/images/p4.png']
const MAX = 10

export function StepMedia({ draft, set, errors }: { draft: Draft; set: (p: Partial<Draft>) => void; errors: Errors }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)

  function addFiles(files: FileList | null) {
    if (!files) return
    const urls = [...files].filter((f) => f.type.startsWith('image/')).map((f) => URL.createObjectURL(f))
    set({ images: [...draft.images, ...urls].slice(0, MAX) })
  }
  const remove = (i: number) => set({ images: draft.images.filter((_, idx) => idx !== i) })
  const makeCover = (i: number) => set({ images: [draft.images[i], ...draft.images.filter((_, idx) => idx !== i)] })

  return (
    <div className="flex flex-col gap-6">
      <div>
        <div
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => {
            e.preventDefault()
            setDragging(false)
            addFiles(e.dataTransfer.files)
          }}
          className={cn(
            'flex flex-col items-center gap-3 rounded-xl border border-dashed bg-card px-6 py-10 text-center transition-colors',
            dragging ? 'border-primary bg-brand-soft/40' : errors.images ? 'border-destructive' : 'border-border',
          )}
        >
          <span className="grid size-12 place-items-center rounded-full bg-brand-soft text-primary">
            <ImagePlus className="size-6" aria-hidden="true" />
          </span>
          <p className="text-sm">
            Drag & drop photos here, or{' '}
            <button type="button" onClick={() => inputRef.current?.click()} className="font-semibold underline underline-offset-4">
              browse files
            </button>
          </p>
          <p className="text-xs text-muted-foreground">
            PNG or JPG, up to {MAX} photos. The first photo is used as the cover.
          </p>
          <input ref={inputRef} type="file" accept="image/*" multiple className="sr-only" onChange={(e) => addFiles(e.target.files)} aria-label="Upload photos" />
          {draft.images.length === 0 && (
            <button type="button" onClick={() => set({ images: SAMPLES })} className="text-xs text-primary hover:underline">
              No photos handy? Use sample photos
            </button>
          )}
        </div>
        {errors.images && <p className="mt-1.5 text-xs text-destructive">{errors.images}</p>}
      </div>

      {draft.images.length > 0 && (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4" aria-label="Uploaded photos">
          {draft.images.map((src, i) => (
            <li key={src + i} className="group relative aspect-[4/3] overflow-hidden rounded-lg border border-border">
              <Image src={src} alt={`Photo ${i + 1}`} fill sizes="200px" className="object-cover" />
              {i === 0 && <span className="absolute left-2 top-2 rounded-full bg-foreground px-2 py-0.5 text-[10px] text-background">Cover</span>}
              <div className="absolute right-2 top-2 flex gap-1 opacity-100 transition-opacity md:opacity-0 md:group-hover:opacity-100 md:group-focus-within:opacity-100">
                {i > 0 && (
                  <button type="button" onClick={() => makeCover(i)} aria-label={`Set photo ${i + 1} as cover`} className="grid size-7 place-items-center rounded-full bg-card shadow">
                    <Star className="size-3.5" />
                  </button>
                )}
                <button type="button" onClick={() => remove(i)} aria-label={`Remove photo ${i + 1}`} className="grid size-7 place-items-center rounded-full bg-card shadow">
                  <X className="size-3.5" />
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div className="grid gap-3 border-t border-border pt-6 sm:grid-cols-2">
        <Field label="Tiêu đề tin đăng *" htmlFor="title" error={errors.title} className="sm:col-span-2">
          <input
            id="title"
            value={draft.title}
            onChange={(e) => set({ title: e.target.value })}
            placeholder="Ví dụ: Biệt Thự Thảo Điền Ven Sông 450m² Full Nội Thất Cao Cấp"
            aria-invalid={!!errors.title}
            className={cn(inputCls, 'border-border')}
          />
        </Field>
        <Field label="Hình thức giao dịch *" htmlFor="listingType">
          <div id="listingType" className="flex gap-2" role="radiogroup">
            {([
              { value: 'sale', label: 'Cần bán' },
              { value: 'rent', label: 'Cho thuê' },
            ] as const).map((t) => (
              <button
                key={t.value}
                type="button"
                role="radio"
                aria-checked={draft.listingType === t.value}
                onClick={() => set({ listingType: t.value })}
                className={cn(
                  'h-10 flex-1 rounded-lg border text-sm font-medium transition-colors',
                  draft.listingType === t.value ? 'border-foreground bg-foreground text-background' : 'border-border bg-card',
                )}
              >
                {t.label}
              </button>
            ))}
          </div>
        </Field>
        <Field
          label={draft.listingType === 'rent' ? 'Giá thuê hàng tháng (VNĐ) *' : 'Giá bán (VNĐ) *'}
          htmlFor="price"
          error={errors.price}
        >
          <input
            id="price"
            type="number"
            min={0}
            inputMode="numeric"
            value={draft.price}
            onChange={(e) => set({ price: e.target.value })}
            placeholder="Ví dụ: 8500000000 (8.5 tỷ)"
            aria-invalid={!!errors.price}
            className={cn(inputCls, 'border-border')}
          />
          {Number(draft.price) > 0 && (
            <p className="mt-1 text-xs text-primary font-medium">
              = {Number(draft.price) >= 1_000_000_000 ? `${(Number(draft.price) / 1_000_000_000).toFixed(2)} tỷ VNĐ` : `${(Number(draft.price) / 1_000_000).toFixed(0)} triệu VNĐ`}
            </p>
          )}
        </Field>
        <Field label="Diện tích sử dụng (m²) *" htmlFor="sqft" error={errors.sqft}>
          <input
            id="sqft"
            type="number"
            min={0}
            inputMode="numeric"
            value={draft.sqft}
            onChange={(e) => set({ sqft: e.target.value, area: e.target.value })}
            placeholder="Ví dụ: 120"
            aria-invalid={!!errors.sqft}
            className={cn(inputCls, 'border-border')}
          />
        </Field>
        <Field label="Mô tả chi tiết BĐS" htmlFor="description" error={errors.description} className="sm:col-span-2">
          <textarea
            id="description"
            rows={4}
            value={draft.description}
            onChange={(e) => set({ description: e.target.value })}
            placeholder="Mô tả chi tiết về vị trí, hướng nhà, pháp lý sổ đỏ, đường trước nhà và các tiện ích liên kết..."
            aria-invalid={!!errors.description}
            className={cn(inputCls, 'h-auto border-border py-2.5')}
          />
        </Field>
      </div>
    </div>
  )
}
