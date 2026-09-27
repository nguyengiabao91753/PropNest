'use client'

import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { Search } from 'lucide-react'
import { cn } from '@/lib/utils'

const modes = [
  { id: 'sale', label: 'Mua nhà' },
  { id: 'rent', label: 'Thuê nhà' },
  { id: 'sell', label: 'Đăng tin bán' },
] as const

export function HomeHero() {
  const router = useRouter()
  const [mode, setMode] = useState<(typeof modes)[number]['id']>('sale')
  const [query, setQuery] = useState('')

  function submit(e: React.FormEvent) {
    e.preventDefault()
    if (mode === 'sell') return router.push('/seller/properties/new')
    const params = new URLSearchParams()
    if (query.trim()) params.set('q', query.trim())
    if (mode === 'sale' || mode === 'rent') params.set('type', mode)
    router.push(`/listings?${params.toString()}`)
  }

  return (
    <section className="mx-auto max-w-7xl px-4 pt-6 md:px-8 md:pt-10">
      <h1 className="text-center font-display text-5xl font-semibold tracking-tight text-balance md:text-8xl">
        Tìm tổ ấm cùng <em className="font-medium">PropNest</em>
      </h1>

      <div role="tablist" aria-label="Search mode" className="mt-6 flex flex-wrap justify-center gap-2">
        {modes.map((m) => (
          <button
            key={m.id}
            role="tab"
            type="button"
            aria-selected={mode === m.id}
            onClick={() => setMode(m.id)}
            className={cn(
              'rounded-full border px-4 py-1.5 text-sm transition-colors',
              mode === m.id
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border bg-card hover:border-primary hover:text-primary',
            )}
          >
            {m.label}
          </button>
        ))}
      </div>

      <div className="relative mt-6">
        <form
          onSubmit={submit}
          className="absolute inset-x-4 -top-4 z-10 mx-auto flex max-w-xl items-center gap-2 rounded-full border border-border bg-surface p-1.5 pl-5 shadow-lg md:-top-6"
        >
          <label htmlFor="hero-search" className="sr-only">
            Tìm kiếm theo địa chỉ, quận, huyện, tỉnh thành
          </label>
          <input
            id="hero-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tìm theo địa chỉ, quận huyện, tên dự án..."
            className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          />
          <button
            type="submit"
            className="inline-flex items-center gap-1.5 rounded-full bg-primary px-4 py-2 text-sm text-primary-foreground transition-colors hover:bg-primary/90"
          >
            <Search className="size-4" aria-hidden="true" />
            Tìm kiếm
          </button>
        </form>
        <div className="relative aspect-[4/3] overflow-hidden rounded-3xl md:aspect-[21/8]">
          <Image
            src="/images/hero-home.png"
            alt="Modern two-storey home glowing at sunset"
            fill
            priority
            sizes="(min-width: 1280px) 1216px, 100vw"
            className="object-cover"
          />
        </div>
      </div>
    </section>
  )
}
