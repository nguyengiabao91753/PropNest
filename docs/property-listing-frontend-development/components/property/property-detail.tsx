'use client'

import Link from 'next/link'
import { SearchX } from 'lucide-react'
import type { Property } from '@/lib/data'
import { useStore } from '@/lib/store'
import { DetailHero } from './detail-hero'
import { DetailBody } from './detail-body'

type Props = {
  id: string
  mode: 'client' | 'seller' | 'admin'
  backHref: string
  toolbar?: (property: Property) => React.ReactNode
}

export function PropertyDetail({ id, mode, backHref, toolbar }: Props) {
  const { getProperty } = useStore()
  const property = getProperty(id)

  const isVisible =
    property &&
    (mode !== 'client' || property.status === 'Published' || (property.status as any) === 'approved')

  if (!property || !isVisible) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3 text-center p-6">
        <SearchX className="size-12 text-muted-foreground" aria-hidden="true" />
        <h2 className="font-display text-2xl font-bold text-foreground">Không tìm thấy bất động sản</h2>
        <p className="text-sm text-muted-foreground max-w-md">
          {property
            ? `Tin đăng này hiện đang ở trạng thái "${property.status}" và chưa được xuất bản công khai.`
            : 'Tin đăng có thể đã bị xóa hoặc đường dẫn không còn tồn tại.'}
        </p>
        <div className="mt-3 flex items-center gap-3">
          <Link href={backHref} className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground hover:bg-primary/90">
            Quay lại danh sách
          </Link>
          {property && mode === 'client' && (
            <Link href="/seller/properties" className="rounded-full border border-border px-5 py-2 text-xs font-semibold text-muted-foreground hover:bg-surface hover:text-foreground">
              Xem trong Cổng Người bán
            </Link>
          )}
        </div>
      </div>
    )
  }

  return (
    <>
      {toolbar?.(property)}
      <DetailHero
        property={property}
        ctaLabel={mode === 'client' ? 'Liên hệ người bán' : 'Chi tiết thông số'}
        onCta={() => document.getElementById(mode === 'client' ? 'inquiry' : 'details')?.scrollIntoView({ behavior: 'smooth' })}
      />
      <DetailBody property={property} previewOnly={mode !== 'client'} />
    </>
  )
}
