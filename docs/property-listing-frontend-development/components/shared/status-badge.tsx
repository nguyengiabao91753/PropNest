import type { ListingStatus, PropertyStatus } from '@/lib/data'
import { cn } from '@/lib/utils'

const styles: Record<string, string> = {
  Draft: 'border-muted-foreground/30 bg-muted/60 text-muted-foreground',
  PendingPayment: 'border-blue-500/40 bg-blue-500/10 text-blue-600 dark:text-blue-400',
  PendingModeration: 'border-amber-500/40 bg-amber-500/10 text-amber-600 dark:text-amber-400',
  Published: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
  Rejected: 'border-rose-500/40 bg-rose-500/10 text-rose-600 dark:text-rose-400',
  Hidden: 'border-purple-500/40 bg-purple-500/10 text-purple-600 dark:text-purple-400',
  Expired: 'border-orange-500/40 bg-orange-500/10 text-orange-600 dark:text-orange-400',
  Deleted: 'border-destructive/40 bg-destructive/10 text-destructive',
  // Legacy aliases
  pending: 'border-amber-500/40 bg-amber-500/10 text-amber-600 dark:text-amber-400',
  approved: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
  rejected: 'border-rose-500/40 bg-rose-500/10 text-rose-600 dark:text-rose-400',
}

const labels: Record<string, string> = {
  Draft: 'Bản nháp',
  PendingPayment: 'Chờ thanh toán',
  PendingModeration: 'Chờ kiểm duyệt',
  Published: 'Đang hiển thị',
  Rejected: 'Bị từ chối',
  Hidden: 'Đã tạm ẩn',
  Expired: 'Hết hạn',
  Deleted: 'Đã xóa',
  // Legacy aliases
  pending: 'Chờ duyệt',
  approved: 'Đang hiển thị',
  rejected: 'Bị từ chối',
}

export function StatusBadge({ status, className }: { status: ListingStatus | PropertyStatus | string; className?: string }) {
  const normalizedStyle = styles[status] || styles.Draft
  const normalizedLabel = labels[status] || status

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap shadow-2xs',
        normalizedStyle,
        className,
      )}
    >
      {normalizedLabel}
    </span>
  )
}

export const STATUS_LABELS = labels
