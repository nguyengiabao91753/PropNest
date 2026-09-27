'use client'

import { useState } from 'react'
import Image from 'next/image'
import Link from 'next/link'
import { Bot, Check, CheckCircle2, ExternalLink, ShieldAlert, ShieldCheck, X, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { StatusBadge } from '@/components/shared/status-badge'
import { formatArea, formatPrice, formatVND } from '@/lib/format'
import { useStore } from '@/lib/store'
import type { Property } from '@/lib/data'

export default function AdminApprovePage() {
  const { properties, moderationApprove, moderationReject, setStatus } = useStore()

  // Listings pending review
  const pending = properties.filter(
    (p) => p.status === 'PendingModeration' || (p.status as any) === 'pending',
  )

  const [selectedId, setSelectedId] = useState<string>(pending[0]?.id || '')
  const [rejectTarget, setRejectTarget] = useState<Property | null>(null)
  const [rejectReason, setRejectReason] = useState('Hình ảnh không rõ ràng và sai lệch thông số diện tích thực tế.')

  const selectedProperty = properties.find((p) => p.id === (selectedId || pending[0]?.id))

  const handleApprove = (p: Property) => {
    if (moderationApprove) moderationApprove(p.id)
    else setStatus(p.id, 'Published')
    toast.success(`Tin đăng ${p.id} đã được phê duyệt và xuất bản!`)
    // Move to next pending if any
    const remaining = pending.filter((x) => x.id !== p.id)
    if (remaining.length > 0) setSelectedId(remaining[0].id)
  }

  const handleRejectConfirm = () => {
    if (!rejectTarget) return
    if (!rejectReason.trim()) {
      toast.error('Vui lòng nhập lý do từ chối tin đăng.')
      return
    }
    if (moderationReject) moderationReject(rejectTarget.id, rejectReason)
    else setStatus(rejectTarget.id, 'Rejected', rejectReason)

    toast.error(`Tin đăng ${rejectTarget.id} đã bị từ chối với lý do: "${rejectReason}"`)
    const remaining = pending.filter((x) => x.id !== rejectTarget.id)
    setRejectTarget(null)
    if (remaining.length > 0) setSelectedId(remaining[0].id)
  }

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      <div>
        <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Kiểm duyệt nội dung</p>
        <h1 className="font-display text-3xl font-bold mt-1">Hàng Đợi Duyệt Tin Đăng (F03)</h1>
        <p className="mt-1 text-xs text-muted-foreground">
          Thẩm định thông tin người bán đăng tải trước khi hiển thị công khai trên trang tìm kiếm.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.1fr_1.3fr]">
        {/* Left Column: Queue list */}
        <section className="overflow-hidden rounded-2xl border border-border bg-card shadow-xs">
          <div className="border-b border-border p-4 flex items-center justify-between bg-surface/50">
            <span className="font-semibold text-sm">Danh sách tin cần duyệt</span>
            <span className="rounded-full bg-amber-500/20 text-amber-700 dark:text-amber-400 font-bold px-2.5 py-0.5 text-xs">
              {pending.length} tin
            </span>
          </div>

          {pending.length === 0 ? (
            <div className="p-12 text-center text-muted-foreground">
              <CheckCircle2 className="size-10 text-emerald-500 mx-auto mb-2" />
              <p className="font-semibold text-sm text-foreground">Hàng đợi kiểm duyệt trống</p>
              <p className="text-xs mt-1">Tất cả các tin đăng mới đã được xử lý xong.</p>
            </div>
          ) : (
            <div className="divide-y divide-border max-h-[700px] overflow-y-auto">
              {pending.map((p) => {
                const isSelected = p.id === (selectedProperty?.id || selectedId)
                return (
                  <button
                    key={p.id}
                    onClick={() => setSelectedId(p.id)}
                    className={`flex w-full items-center gap-4 p-4 text-left transition hover:bg-surface ${
                      isSelected ? 'bg-brand-soft/60 border-l-4 border-l-primary' : ''
                    }`}
                  >
                    <div className="relative size-16 rounded-xl overflow-hidden shrink-0 border border-border">
                      <Image src={p.images[0] || '/images/p1.png'} alt="" fill className="object-cover" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="font-mono text-[11px] text-muted-foreground font-semibold">{p.id}</span>
                        <StatusBadge status={p.status} />
                        {p.packageCode === 'VIP' && (
                          <span className="rounded bg-amber-500 text-white text-[9px] font-bold px-1.5 py-0.2">
                            VIP
                          </span>
                        )}
                      </div>
                      <p className="truncate font-semibold text-sm text-foreground">{p.title}</p>
                      <p className="text-xs text-muted-foreground truncate">
                        {[p.district, p.city].filter(Boolean).join(', ')} · {p.sellerName}
                      </p>
                      <p className="text-xs font-bold text-primary mt-1">{formatPrice(p)}</p>
                    </div>
                  </button>
                )
              })}
            </div>
          )}
        </section>

        {/* Right Column: Active Review Card */}
        {selectedProperty ? (
          <section className="rounded-2xl border border-border bg-card p-6 shadow-xs space-y-6">
            <div className="relative aspect-[16/9] w-full rounded-xl overflow-hidden border border-border">
              <Image src={selectedProperty.images[0] || '/images/p1.png'} alt={selectedProperty.title} fill className="object-cover" />
              <span className="absolute left-3 top-3 bg-black/70 backdrop-blur-xs text-white text-xs px-3 py-1 rounded-full font-medium">
                {selectedProperty.listingType === 'sale' ? 'Cần bán' : 'Cho thuê'} · {selectedProperty.propertyType}
              </span>
            </div>

            <div className="flex items-start justify-between gap-4">
              <div>
                <span className="font-mono text-xs text-muted-foreground font-semibold block">{selectedProperty.id}</span>
                <h2 className="font-display text-2xl font-bold mt-1 text-foreground">{selectedProperty.title}</h2>
                <p className="text-xs text-muted-foreground mt-1">
                  📍 {[selectedProperty.address, selectedProperty.ward, selectedProperty.district, selectedProperty.city].filter(Boolean).join(', ')}
                </p>
              </div>
              <div className="text-right">
                <span className="text-2xl font-extrabold text-primary font-display">{formatPrice(selectedProperty)}</span>
                <span className="text-xs text-muted-foreground block">Người đăng: {selectedProperty.sellerName}</span>
              </div>
            </div>

            <div className="grid grid-cols-4 gap-3 rounded-xl bg-surface p-4 text-center text-xs">
              <div>
                <strong className="text-sm block">{selectedProperty.beds}</strong>
                <span className="text-muted-foreground">Phòng ngủ</span>
              </div>
              <div>
                <strong className="text-sm block">{selectedProperty.baths}</strong>
                <span className="text-muted-foreground">Phòng tắm</span>
              </div>
              <div>
                <strong className="text-sm block">{formatArea(selectedProperty.area || selectedProperty.sqft)}</strong>
                <span className="text-muted-foreground">Diện tích</span>
              </div>
              <div>
                <strong className="text-sm block text-amber-600 font-bold">{selectedProperty.packageCode || 'Standard'}</strong>
                <span className="text-muted-foreground">Gói hiển thị</span>
              </div>
            </div>

            {/* Description */}
            <div>
              <h4 className="text-xs font-semibold uppercase text-muted-foreground mb-1">Mô tả bất động sản</h4>
              <p className="text-xs text-muted-foreground leading-relaxed bg-surface/50 p-3 rounded-xl border border-border/60">
                {selectedProperty.description}
              </p>
            </div>

            {/* AME AI Recommendation Widget (F09) */}
            <div className="rounded-xl border border-primary/30 bg-brand-soft/60 p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-primary flex items-center gap-1.5">
                  <Bot className="size-4" /> AME Auto-Moderation Engine (Tư vấn AI)
                </span>
                <span className="text-[10px] rounded bg-emerald-500/20 text-emerald-700 font-semibold px-2 py-0.5">
                  Khuyên duyệt (96% tin cậy)
                </span>
              </div>
              <div className="grid sm:grid-cols-3 gap-2 text-xs text-muted-foreground pt-1">
                <div>
                  <span className="block text-[11px]">Từ khóa nhạy cảm:</span>
                  <strong className="text-emerald-600 font-medium">Hợp lệ (Không vi phạm)</strong>
                </div>
                <div>
                  <span className="block text-[11px]">Benchmark giá:</span>
                  <strong className="text-emerald-600 font-medium">Chuẩn khu vực (0.98x)</strong>
                </div>
                <div>
                  <span className="block text-[11px]">Chất lượng ảnh:</span>
                  <strong className="text-emerald-600 font-medium">Đạt tiêu chuẩn HD</strong>
                </div>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex items-center gap-3 pt-2">
              <Button
                className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs py-3"
                onClick={() => handleApprove(selectedProperty)}
              >
                <Check className="size-4 mr-1.5" /> Phê duyệt tin đăng
              </Button>
              <Button
                variant="destructive"
                className="flex-1 font-semibold text-xs py-3"
                onClick={() => setRejectTarget(selectedProperty)}
              >
                <X className="size-4 mr-1.5" /> Từ chối duyệt (Lý do)
              </Button>
            </div>
          </section>
        ) : (
          <div className="rounded-2xl border border-dashed border-border bg-card/60 p-12 text-center text-muted-foreground text-xs flex flex-col items-center justify-center min-h-[400px]">
            Chọn một tin đăng từ danh sách bên trái để xem chi tiết và thẩm định.
          </div>
        )}
      </div>

      {/* Reject Modal with Mandatory Reason */}
      <Dialog open={!!rejectTarget} onOpenChange={(o) => !o && setRejectTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="text-destructive flex items-center gap-2">
              <ShieldAlert className="size-5" /> Từ chối duyệt tin: {rejectTarget?.id}
            </DialogTitle>
            <DialogDescription>
              Theo quy định Blueprint, từ chối tin đăng <strong>bắt buộc phải có lý do cụ thể</strong> để gửi thông báo phản hồi cho người bán.
            </DialogDescription>
          </DialogHeader>

          <div className="py-3">
            <label className="text-xs font-semibold text-foreground mb-1.5 block">Lý do từ chối (Reason) *</label>
            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Nhập lý do vi phạm (ví dụ: Hình ảnh mờ, số điện thoại spam, giá ảo...)"
              className="w-full rounded-lg border border-border bg-card p-3 text-sm outline-none focus:border-destructive"
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTarget(null)}>
              Hủy
            </Button>
            <Button variant="destructive" onClick={handleRejectConfirm}>
              Xác nhận từ chối tin
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
