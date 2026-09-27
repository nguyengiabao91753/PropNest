'use client'

import Image from 'next/image'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { ChevronDown, ChevronUp, Eye, EyeOff, History, MoreHorizontal, Pencil, Send, Trash2 } from 'lucide-react'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { StatusBadge } from '@/components/shared/status-badge'
import type { Property } from '@/lib/data'
import { completion, formatArea, formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

export type SortField = 'price' | 'sqft' | 'status'
export type SortState = { field: SortField; dir: 'asc' | 'desc' } | null

function Completion({ value }: { value: number }) {
  const color = value >= 60 ? 'bg-emerald-500' : value >= 35 ? 'bg-amber-500' : 'bg-rose-500'
  return (
    <div className="flex items-center gap-3">
      <div
        className="h-1.5 w-24 overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Listing completeness"
      >
        <div className={cn('h-full rounded-full transition-all', color)} style={{ width: `${value}%` }} />
      </div>
      <span className="w-9 text-xs tabular-nums text-muted-foreground">{value}%</span>
    </div>
  )
}

export function RowActions({
  property,
  onDelete,
  onViewHistory,
  onSubmitForModeration,
  onToggleHide,
}: {
  property: Property
  onDelete: (p: Property) => void
  onViewHistory?: (p: Property) => void
  onSubmitForModeration?: (p: Property) => void
  onToggleHide?: (p: Property) => void
}) {
  const router = useRouter()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className="grid size-8 place-items-center rounded-md outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
        aria-label={`Actions for ${property.title}`}
      >
        <MoreHorizontal className="size-5 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuItem onClick={() => router.push(`/properties/${property.id}`)}>
          <Eye className="size-4" /> Xem bài đăng công khai
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => router.push(`/seller/properties/${property.id}/edit`)}>
          <Pencil className="size-4" /> Chỉnh sửa tin
        </DropdownMenuItem>

        {onViewHistory && (
          <DropdownMenuItem onClick={() => onViewHistory(property)}>
            <History className="size-4 text-primary" /> Lịch sử thay đổi (Delta)
          </DropdownMenuItem>
        )}

        {property.status === 'Draft' && onSubmitForModeration && (
          <DropdownMenuItem onClick={() => onSubmitForModeration(property)} className="text-amber-600">
            <Send className="size-4" /> Gửi duyệt tin
          </DropdownMenuItem>
        )}

        {onToggleHide && (property.status === 'Published' || property.status === 'Hidden') && (
          <DropdownMenuItem onClick={() => onToggleHide(property)}>
            {property.status === 'Published' ? (
              <>
                <EyeOff className="size-4 text-muted-foreground" /> Tạm ẩn tin
              </>
            ) : (
              <>
                <Eye className="size-4 text-emerald-600" /> Hiện tin trở lại
              </>
            )}
          </DropdownMenuItem>
        )}

        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={() => onDelete(property)}>
          <Trash2 className="size-4" /> Xóa tin đăng
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function SortHeader({
  label,
  field,
  sort,
  onSort,
}: {
  label: string
  field: SortField
  sort: SortState
  onSort: (f: SortField) => void
}) {
  const active = sort?.field === field
  return (
    <th scope="col" className="px-4 py-3 font-medium text-xs text-muted-foreground uppercase" aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button type="button" onClick={() => onSort(field)} className="inline-flex items-center gap-1 hover:text-foreground">
        {label}
        {active && sort.dir === 'asc' ? <ChevronUp className="size-3.5" /> : <ChevronDown className={cn('size-3.5', active && 'text-primary')} />}
      </button>
    </th>
  )
}

export function PropertyTable({
  rows,
  sort,
  onSort,
  onDelete,
  onViewHistory,
  onSubmitForModeration,
  onToggleHide,
}: {
  rows: Property[]
  sort: SortState
  onSort: (f: SortField) => void
  onDelete: (p: Property) => void
  onViewHistory?: (p: Property) => void
  onSubmitForModeration?: (p: Property) => void
  onToggleHide?: (p: Property) => void
}) {
  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-card">
      <table className="w-full min-w-[950px] text-left">
        <thead className="border-b border-border text-xs text-muted-foreground bg-surface/50">
          <tr>
            <th scope="col" className="px-4 py-3 font-medium uppercase">
              Bất động sản
            </th>
            <th scope="col" className="px-4 py-3 font-medium uppercase">
              Mã tin
            </th>
            <SortHeader label="Trạng thái" field="status" sort={sort} onSort={onSort} />
            <th scope="col" className="px-4 py-3 font-medium uppercase">
              Gói tin
            </th>
            <SortHeader label="Giá niêm yết" field="price" sort={sort} onSort={onSort} />
            <SortHeader label="Diện tích" field="sqft" sort={sort} onSort={onSort} />
            <th scope="col" className="px-4 py-3 font-medium uppercase">
              Độ hoàn thiện
            </th>
            <th scope="col" className="px-4 py-3">
              <span className="sr-only">Thao tác</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border text-sm">
          {rows.map((p) => (
            <tr key={p.id} className="transition-colors hover:bg-surface/60">
              <td className="px-4 py-3.5">
                <Link href={`/properties/${p.id}`} className="flex items-center gap-3 hover:text-primary">
                  <span className="relative size-10 shrink-0 overflow-hidden rounded-lg border border-border">
                    <Image src={p.images[0] || '/images/p1.png'} alt="" fill sizes="40px" className="object-cover" />
                  </span>
                  <div>
                    <span className="max-w-64 truncate font-medium block text-foreground">{p.title}</span>
                    <span className="text-xs text-muted-foreground line-clamp-1">
                      {[p.address, p.ward, p.district].filter(Boolean).join(', ')}
                    </span>
                  </div>
                </Link>
              </td>
              <td className="px-4 py-3.5 font-mono text-xs text-muted-foreground">{p.id}</td>
              <td className="px-4 py-3.5">
                <StatusBadge status={p.status} />
                {p.status === 'Rejected' && p.rejectReason && (
                  <p className="mt-1 max-w-48 truncate text-[11px] text-rose-600 font-medium" title={p.rejectReason}>
                    Lý do: {p.rejectReason}
                  </p>
                )}
              </td>
              <td className="px-4 py-3.5">
                <span
                  className={cn(
                    'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold',
                    p.packageCode === 'VIP'
                      ? 'bg-amber-500/15 text-amber-700 dark:text-amber-400 border border-amber-500/30'
                      : p.packageCode === 'Boost'
                        ? 'bg-blue-500/15 text-blue-700 dark:text-blue-400 border border-blue-500/30'
                        : 'bg-muted text-muted-foreground',
                  )}
                >
                  {p.packageCode || 'Standard'}
                </span>
              </td>
              <td className="px-4 py-3.5 tabular-nums font-semibold text-foreground">{formatPrice(p)}</td>
              <td className="px-4 py-3.5 tabular-nums text-muted-foreground">{formatArea(p.area || p.sqft)}</td>
              <td className="px-4 py-3.5">
                <Completion value={completion(p)} />
              </td>
              <td className="px-4 py-3.5 text-right">
                <RowActions
                  property={p}
                  onDelete={onDelete}
                  onViewHistory={onViewHistory}
                  onSubmitForModeration={onSubmitForModeration}
                  onToggleHide={onToggleHide}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
