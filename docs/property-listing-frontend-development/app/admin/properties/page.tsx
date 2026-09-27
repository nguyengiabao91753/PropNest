'use client'

import { useMemo, useState } from 'react'
import Image from 'next/image'
import Link from 'next/link'
import { ChevronDown, ExternalLink, Search } from 'lucide-react'
import { StatusBadge } from '@/components/shared/status-badge'
import { formatArea, formatPrice } from '@/lib/format'
import { useStore } from '@/lib/store'
import type { ListingStatus, Property } from '@/lib/data'
import { cn } from '@/lib/utils'

export default function AdminPropertiesPage() {
  const { properties } = useStore()
  const [filterStatus, setFilterStatus] = useState<string>('all')
  const [q, setQ] = useState('')

  const statusList: { key: string; label: string }[] = [
    { key: 'all', label: 'Tất cả' },
    { key: 'Published', label: 'Đang hiển thị' },
    { key: 'PendingModeration', label: 'Chờ kiểm duyệt' },
    { key: 'PendingPayment', label: 'Chờ thanh toán' },
    { key: 'Draft', label: 'Bản nháp' },
    { key: 'Rejected', label: 'Bị từ chối' },
    { key: 'Hidden', label: 'Đã ẩn' },
    { key: 'Expired', label: 'Hết hạn' },
  ]

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase()
    return properties.filter((p) => {
      if (filterStatus !== 'all') {
        if (filterStatus === 'Published' && p.status !== 'Published' && (p.status as any) !== 'approved') return false
        if (filterStatus === 'PendingModeration' && p.status !== 'PendingModeration' && (p.status as any) !== 'pending') return false
        if (filterStatus === 'Rejected' && p.status !== 'Rejected' && (p.status as any) !== 'rejected') return false
        if (filterStatus !== 'Published' && filterStatus !== 'PendingModeration' && filterStatus !== 'Rejected' && p.status !== filterStatus) return false
      }
      if (term && ![p.id, p.title, p.sellerName, p.district, p.city].some((s) => s && s.toLowerCase().includes(term))) {
        return false
      }
      return true
    })
  }, [properties, filterStatus, q])

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Quản lý kho dữ liệu</p>
          <h1 className="font-display text-3xl font-bold mt-1">Toàn Bộ Tin Đăng Hệ Thống</h1>
        </div>
        <span className="text-xs text-muted-foreground">
          Tổng cộng: <strong>{properties.length}</strong> tin đăng trong cơ sở dữ liệu
        </span>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-1.5">
          {statusList.map((s) => (
            <button
              key={s.key}
              onClick={() => setFilterStatus(s.key)}
              className={cn(
                'rounded-full px-3 py-1 text-xs font-medium transition-colors',
                filterStatus === s.key
                  ? 'bg-primary text-primary-foreground font-semibold shadow-xs'
                  : 'bg-card border border-border text-muted-foreground hover:text-foreground',
              )}
            >
              {s.label}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-1.5 text-xs w-64">
          <Search className="size-4 text-muted-foreground" />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Tìm mã PN-, tiêu đề, môi giới..."
            className="w-full bg-transparent outline-none"
          />
        </div>
      </div>

      <div className="rounded-2xl border border-border bg-card overflow-hidden shadow-xs">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-surface/50 text-xs text-muted-foreground border-b border-border uppercase">
              <tr>
                <th className="px-4 py-3">Bất động sản</th>
                <th className="px-4 py-3">Mã tin</th>
                <th className="px-4 py-3">Trạng thái</th>
                <th className="px-4 py-3">Người đăng</th>
                <th className="px-4 py-3">Gói</th>
                <th className="px-4 py-3">Giá</th>
                <th className="px-4 py-3">Diện tích</th>
                <th className="px-4 py-3 text-right">Xem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {filtered.map((p) => (
                <tr key={p.id} className="hover:bg-surface/60 transition-colors">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <div className="relative size-10 rounded-lg overflow-hidden border border-border shrink-0">
                        <Image src={p.images[0] || '/images/p1.png'} alt="" fill className="object-cover" />
                      </div>
                      <div className="min-w-0">
                        <p className="font-medium text-foreground truncate max-w-64">{p.title}</p>
                        <p className="text-xs text-muted-foreground truncate">{[p.district, p.city].filter(Boolean).join(', ')}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-muted-foreground">{p.id}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={p.status} />
                  </td>
                  <td className="px-4 py-3 text-xs text-foreground font-medium">{p.sellerName}</td>
                  <td className="px-4 py-3">
                    <span className="text-xs font-semibold">{p.packageCode || 'Standard'}</span>
                  </td>
                  <td className="px-4 py-3 font-semibold text-primary">{formatPrice(p)}</td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">{formatArea(p.area || p.sqft)}</td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      href={`/properties/${p.id}`}
                      target="_blank"
                      className="inline-flex items-center gap-1 text-xs text-primary hover:underline"
                    >
                      <ExternalLink className="size-3.5" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
