'use client'

import Link from 'next/link'
import { ArrowUpRight, Building2, CheckSquare, Clock, DollarSign, ShieldAlert, Users } from 'lucide-react'
import { useStore } from '@/lib/store'
import { formatCompactPrice, formatPrice, formatVND } from '@/lib/format'
import { StatusBadge } from '@/components/shared/status-badge'

export default function AdminPage() {
  const { properties, transactions } = useStore()

  const pending = properties.filter(
    (p) => p.status === 'PendingModeration' || (p.status as any) === 'pending',
  )
  const published = properties.filter(
    (p) => p.status === 'Published' || (p.status as any) === 'approved',
  )
  const rejected = properties.filter(
    (p) => p.status === 'Rejected' || (p.status as any) === 'rejected',
  )
  const activeSellers = new Set(properties.map((p) => p.sellerId)).size

  // Calculate revenue from charges in transactions
  const totalRevenue = transactions
    .filter((t) => t.type === 'Charge')
    .reduce((acc, t) => acc + Math.abs(t.amount), 0)

  const cards = [
    {
      label: 'Tổng tin đăng',
      value: properties.length,
      sub: `${published.length} tin đang hiển thị`,
      icon: Building2,
      color: 'text-primary',
    },
    {
      label: 'Tin chờ kiểm duyệt',
      value: pending.length,
      sub: pending.length > 0 ? 'Cần xử lý ngay' : 'Đã duyệt hết',
      icon: Clock,
      color: 'text-amber-500',
    },
    {
      label: 'Số môi giới / Người bán',
      value: activeSellers,
      sub: 'Đang hoạt động trên hệ thống',
      icon: Users,
      color: 'text-blue-500',
    },
    {
      label: 'Doanh thu dịch vụ (Gói)',
      value: totalRevenue > 0 ? formatVND(totalRevenue) : '700.000 ₫',
      sub: 'Từ gói VIP & Boost tin',
      icon: DollarSign,
      color: 'text-emerald-500',
    },
  ]

  return (
    <div className="mx-auto max-w-[1400px] space-y-8">
      <div>
        <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Tổng quan hệ thống</p>
        <h1 className="font-display text-3xl font-bold mt-1">Bảng Điều Khiển Quản Trị</h1>
      </div>

      {/* KPI Cards */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cards.map((c) => {
          const Icon = c.icon
          return (
            <div key={c.label} className="rounded-2xl border border-border bg-card p-5 shadow-xs">
              <div className="flex items-center justify-between text-sm text-muted-foreground">
                <span className="font-medium text-xs">{c.label}</span>
                <Icon className={`size-5 ${c.color}`} />
              </div>
              <p className="mt-4 font-display text-3xl font-bold text-foreground">{c.value}</p>
              <p className="mt-1 text-xs text-muted-foreground">{c.sub}</p>
            </div>
          )
        })}
      </div>

      {/* Main Grid: Recent Listings & Pending Queue Banner */}
      <div className="grid gap-6 xl:grid-cols-[1.6fr_1fr]">
        {/* Recent Listings */}
        <section className="rounded-2xl border border-border bg-card overflow-hidden shadow-xs">
          <div className="flex items-center justify-between border-b border-border p-5">
            <div>
              <h2 className="font-semibold text-base">Tin đăng mới cập nhật</h2>
              <p className="text-xs text-muted-foreground mt-0.5">Danh sách các tin đăng được gửi lên gần đây nhất</p>
            </div>
            <Link href="/admin/properties" className="inline-flex items-center gap-1 text-xs text-primary font-medium hover:underline">
              Xem toàn bộ <ArrowUpRight className="size-3.5" />
            </Link>
          </div>
          <div className="divide-y divide-border">
            {properties.slice(0, 6).map((p) => (
              <div key={p.id} className="flex items-center justify-between gap-4 p-4 text-sm hover:bg-surface transition-colors">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-mono text-xs text-muted-foreground">{p.id}</span>
                    <StatusBadge status={p.status} />
                    {p.packageCode === 'VIP' && (
                      <span className="rounded bg-amber-500/20 text-amber-700 dark:text-amber-400 px-1.5 py-0.2 text-[10px] font-bold">
                        VIP
                      </span>
                    )}
                  </div>
                  <p className="font-medium truncate text-foreground">{p.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {[p.district, p.city].filter(Boolean).join(', ')} · Người đăng: {p.sellerName}
                  </p>
                </div>
                <div className="text-right shrink-0">
                  <span className="font-semibold text-foreground block">{formatCompactPrice(p)}</span>
                  <span className="text-xs text-muted-foreground">{p.area || p.sqft} m²</span>
                </div>
              </div>
            ))}
          </div>
        </section>

        {/* Approval Queue Banner & Action Card */}
        <section className="space-y-6">
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/5 p-6 shadow-xs">
            <div className="flex items-center gap-2 text-amber-600 font-semibold text-xs uppercase tracking-wider mb-2">
              <CheckSquare className="size-4" /> Hàng đợi kiểm duyệt tin (F03)
            </div>
            <h3 className="font-display text-2xl font-bold text-foreground">
              {pending.length} tin chờ duyệt
            </h3>
            <p className="mt-2 text-xs text-muted-foreground leading-relaxed">
              Kiểm tra tính pháp lý, vị trí và thông số kỹ thuật bất động sản trước khi xuất bản rộng rãi cho khách hàng.
            </p>
            <div className="mt-6">
              <Link
                href="/admin/approve"
                className="inline-flex items-center justify-center w-full rounded-xl bg-primary text-primary-foreground py-2.5 text-xs font-semibold hover:bg-primary/90 transition-colors shadow-xs"
              >
                Mở hàng đợi duyệt ngay
              </Link>
            </div>
          </div>

          <div className="rounded-2xl border border-border bg-card p-6 shadow-xs space-y-3">
            <h4 className="font-semibold text-sm">Chính sách kiểm duyệt PropNest</h4>
            <ul className="space-y-2 text-xs text-muted-foreground">
              <li className="flex items-start gap-2">
                <span className="size-1.5 rounded-full bg-emerald-500 mt-1.5 shrink-0" />
                <span>Mọi tin đăng phải có đầy đủ Quận/Huyện, Tỉnh/Thành phố và diện tích hợp lệ.</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="size-1.5 rounded-full bg-emerald-500 mt-1.5 shrink-0" />
                <span>Từ chối tin đăng <strong>bắt buộc phải có lý do cụ thể</strong> để lưu vết Audit Log.</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="size-1.5 rounded-full bg-emerald-500 mt-1.5 shrink-0" />
                <span>Auto-Moderation Engine (AME AI) tự động quét Benchmark giá và phát hiện spam.</span>
              </li>
            </ul>
          </div>
        </section>
      </div>
    </div>
  )
}
