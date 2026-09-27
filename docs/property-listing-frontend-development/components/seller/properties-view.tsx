'use client'

import Link from 'next/link'
import { useMemo, useState } from 'react'
import { Bell, ChevronDown, FileText, Grid3x3, MessageCircleMore, Plus, Rows3, Search } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { Checkbox } from '@/components/ui/checkbox'
import { CURRENT_SELLER, PROPERTY_TYPES, type ListingStatus, type Property, type PropertyType } from '@/lib/data'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'
import { PropertyTable, type SortField, type SortState } from './property-table'
import { PropertyGrid } from './property-grid'
import { HistoryDeltaDialog } from './history-delta-dialog'

type StatusFilter = 'all' | ListingStatus

const statusOptions: { value: StatusFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'Published', label: 'Đang hiển thị (Published)' },
  { value: 'PendingModeration', label: 'Chờ duyệt (PendingModeration)' },
  { value: 'Draft', label: 'Bản nháp (Draft)' },
  { value: 'PendingPayment', label: 'Chờ thanh toán (PendingPayment)' },
  { value: 'Rejected', label: 'Bị từ chối (Rejected)' },
  { value: 'Hidden', label: 'Đã tạm ẩn (Hidden)' },
  { value: 'Expired', label: 'Hết hạn (Expired)' },
]

export function SellerPropertiesView({ initialStatus = 'all' }: { initialStatus?: StatusFilter }) {
  const { properties, deleteProperty, submitListing, hideListing, unhideListing } = useStore()
  const [status, setStatus] = useState<StatusFilter>(initialStatus)
  const [q, setQ] = useState('')
  const [types, setTypes] = useState<PropertyType[]>([])
  const [listing, setListing] = useState<'all' | 'sale' | 'rent'>('all')
  const [sort, setSort] = useState<SortState>(null)
  const [layout, setLayout] = useState<'table' | 'grid'>('table')
  const [perPage, setPerPage] = useState(10)
  const [page, setPage] = useState(1)
  const [toDelete, setToDelete] = useState<Property | null>(null)
  const [historyTarget, setHistoryTarget] = useState<Property | null>(null)

  // Seller's own properties
  const mine = useMemo(() => properties.filter((p) => p.sellerId === CURRENT_SELLER.id), [properties])

  const counts = useMemo(
    () => ({
      all: mine.length,
      Published: mine.filter((p) => p.status === 'Published' || (p.status as any) === 'approved').length,
      PendingModeration: mine.filter((p) => p.status === 'PendingModeration' || (p.status as any) === 'pending').length,
      Draft: mine.filter((p) => p.status === 'Draft').length,
      PendingPayment: mine.filter((p) => p.status === 'PendingPayment').length,
      Rejected: mine.filter((p) => p.status === 'Rejected' || (p.status as any) === 'rejected').length,
      Hidden: mine.filter((p) => p.status === 'Hidden').length,
      Expired: mine.filter((p) => p.status === 'Expired').length,
    }),
    [mine],
  )

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase()
    const list = mine.filter((p) => {
      if (status !== 'all') {
        if (status === 'Published' && p.status !== 'Published' && (p.status as any) !== 'approved') return false
        if (status === 'PendingModeration' && p.status !== 'PendingModeration' && (p.status as any) !== 'pending') return false
        if (status === 'Rejected' && p.status !== 'Rejected' && (p.status as any) !== 'rejected') return false
        if (status !== 'Published' && status !== 'PendingModeration' && status !== 'Rejected' && p.status !== status)
          return false
      }
      if (listing !== 'all' && p.listingType !== listing) return false
      if (types.length && !types.includes(p.propertyType)) return false
      if (
        term &&
        ![p.id, p.title, p.address, p.ward, p.district, p.city, p.suburb].some((s) => s && s.toLowerCase().includes(term))
      )
        return false
      return true
    })

    if (!sort) return list
    const mul = sort.dir === 'asc' ? 1 : -1
    return [...list].sort((a, b) => {
      if (sort.field === 'price') return (a.price - b.price) * mul
      if (sort.field === 'sqft') return ((a.area || a.sqft) - (b.area || b.sqft)) * mul
      return a.status.localeCompare(b.status) * mul
    })
  }, [mine, status, listing, types, q, sort])

  const totalPages = Math.max(1, Math.ceil(filtered.length / perPage))
  const current = Math.min(page, totalPages)
  const rows = filtered.slice((current - 1) * perPage, current * perPage)

  const reset = () => setPage(1)
  const onSort = (field: SortField) =>
    setSort((s) => (s?.field !== field ? { field, dir: 'asc' } : s.dir === 'asc' ? { field, dir: 'desc' } : null))

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 md:px-8">
      <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-border px-5 py-5 md:px-8">
          <div>
            <h1 className="font-display text-3xl font-bold">Quản lý Bất động sản</h1>
            <p className="text-xs text-muted-foreground mt-1">
              Theo dõi vòng đời tin đăng, cập nhật giá và kiểm toán lịch sử thay đổi (Delta Engine).
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Link href="/seller/properties/new">
              <Button size="sm" className="gap-2 bg-primary text-primary-foreground font-semibold">
                <Plus className="size-4" /> Đăng tin mới
              </Button>
            </Link>
          </div>
        </div>

        <div className="flex flex-col gap-5 p-5 md:p-8">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-3">
              <DropdownMenu>
                <DropdownMenuTrigger className="inline-flex h-10 items-center gap-3 rounded-lg border border-border px-4 text-sm font-medium outline-none hover:border-primary">
                  {statusOptions.find((s) => s.value === status)?.label}
                  <ChevronDown className="size-4" aria-hidden="true" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" className="w-64">
                  {statusOptions.map((s) => (
                    <DropdownMenuItem
                      key={s.value}
                      onClick={() => {
                        setStatus(s.value)
                        reset()
                      }}
                      className={cn('justify-between text-xs', status === s.value && 'text-primary font-semibold')}
                    >
                      {s.label}
                      <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground font-mono">
                        {(counts as any)[s.value] ?? counts.all}
                      </span>
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>

              <label className="flex h-10 items-center gap-2 rounded-lg border border-border px-3 focus-within:border-primary">
                <Search className="size-4 text-muted-foreground" aria-hidden="true" />
                <span className="sr-only">Tìm theo mã tin hoặc vị trí</span>
                <input
                  value={q}
                  onChange={(e) => {
                    setQ(e.target.value)
                    reset()
                  }}
                  placeholder="Tìm mã PN-, địa chỉ, quận..."
                  className="w-48 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
                />
              </label>
            </div>

            {/* Layout switch */}
            <div className="flex items-center gap-1 rounded-lg border border-border p-1 bg-surface">
              <button
                type="button"
                onClick={() => setLayout('table')}
                className={cn('rounded p-1.5 transition-colors', layout === 'table' ? 'bg-card shadow-xs text-foreground' : 'text-muted-foreground')}
                aria-label="Table view"
              >
                <Rows3 className="size-4" />
              </button>
              <button
                type="button"
                onClick={() => setLayout('grid')}
                className={cn('rounded p-1.5 transition-colors', layout === 'grid' ? 'bg-card shadow-xs text-foreground' : 'text-muted-foreground')}
                aria-label="Grid view"
              >
                <Grid3x3 className="size-4" />
              </button>
            </div>
          </div>

          {/* Table or Grid */}
          {layout === 'table' ? (
            <PropertyTable
              rows={rows}
              sort={sort}
              onSort={onSort}
              onDelete={(p) => setToDelete(p)}
              onViewHistory={(p) => setHistoryTarget(p)}
              onSubmitForModeration={(p) => {
                submitListing(p.id)
                toast.success(`Tin ${p.id} đã gửi vào hàng đợi duyệt thành công!`)
              }}
              onToggleHide={(p) => {
                if (p.status === 'Published') {
                  hideListing(p.id)
                  toast(`Tin ${p.id} đã tạm ẩn khỏi trang tìm kiếm.`)
                } else {
                  unhideListing(p.id)
                  toast.success(`Tin ${p.id} đã hiển thị công khai trở lại.`)
                }
              }}
            />
          ) : (
            <PropertyGrid rows={rows} onDelete={(p) => setToDelete(p)} />
          )}

          {/* Pagination */}
          <div className="flex flex-wrap items-center justify-between gap-4 pt-4 border-t border-border text-xs text-muted-foreground">
            <span>
              Hiển thị {rows.length} trên tổng số {filtered.length} tin đăng
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={current <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Trang trước
              </Button>
              <span className="font-medium text-foreground">
                {current} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={current >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                Trang sau
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Delete confirmation dialog */}
      <Dialog open={!!toDelete} onOpenChange={(open) => !open && setToDelete(null)}>
        <DialogContent>
          <DialogTitle>Xác nhận xóa tin đăng?</DialogTitle>
          <DialogDescription>
            Bạn có chắc chắn muốn xóa tin đăng <strong>{toDelete?.title}</strong> ({toDelete?.id}) không? Hành động này không thể hoàn tác.
          </DialogDescription>
          <DialogFooter className="gap-2 sm:gap-0">
            <Button variant="outline" onClick={() => setToDelete(null)}>
              Hủy bỏ
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                if (toDelete) {
                  deleteProperty(toDelete.id)
                  toast.success(`Đã xóa tin đăng ${toDelete.id}`)
                  setToDelete(null)
                }
              }}
            >
              Xác nhận xóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* History Delta dialog */}
      <HistoryDeltaDialog
        property={historyTarget}
        open={!!historyTarget}
        onOpenChange={(open) => !open && setHistoryTarget(null)}
      />
    </div>
  )
}
