'use client'

import { Pencil, ShieldCheck } from 'lucide-react'
import { PACKAGES } from '@/lib/data'
import { formatArea, formatPrice, formatVND } from '@/lib/format'
import { SquareCheck, type Draft, type Errors } from './fields'
import { PreviewCard } from './preview-card'

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 py-2 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{value || '—'}</dd>
    </div>
  )
}

export function StepSubmit({
  draft,
  confirmed,
  onConfirm,
  errors,
  onEdit,
}: {
  draft: Draft
  confirmed: boolean
  onConfirm: (c: boolean) => void
  errors: Errors
  onEdit: (step: number) => void
}) {
  const facilities = draft.facilities.filter((f) => f !== 'None')
  const selectedPkg = PACKAGES.find((p) => p.code === draft.packageCode) || PACKAGES[0]

  const sections = [
    {
      title: '01. Thông tin Bất động sản',
      step: 0,
      rows: [
        ['Địa chỉ', `${draft.streetNumber ? `Số ${draft.streetNumber}, ` : ''}${draft.address}`.trim()],
        ['Khu vực hành chính', [draft.ward, draft.district, draft.city].filter(Boolean).join(' · ')],
        ['Loại BĐS & Tình trạng', [draft.propertyType, draft.condition].filter(Boolean).join(' · ')],
        ['Phòng ngủ / Tắm / Ô tô', `${draft.beds} PN / ${draft.baths} WC / ${draft.carports} chỗ`],
        ['Diện tích', draft.sqft || draft.area ? formatArea(Number(draft.sqft || draft.area)) : '—'],
        ['Tiện ích', facilities.join(', ') || 'Cơ bản'],
      ],
    },
    {
      title: '02. Hình ảnh & Giá niêm yết',
      step: 1,
      rows: [
        ['Tiêu đề tin', draft.title || 'Chưa đặt tiêu đề'],
        [
          'Giá niêm yết',
          draft.price
            ? formatPrice({ price: Number(draft.price), listingType: draft.listingType })
            : '0 ₫',
        ],
        ['Hình thức', draft.listingType === 'sale' ? 'Cần bán' : 'Cho thuê'],
        ['Hình ảnh tải lên', `${draft.images.length} ảnh đã sẵn sàng`],
      ],
    },
    {
      title: '03. Gói dịch vụ hiển thị',
      step: 3,
      rows: [
        ['Gói đã chọn', selectedPkg.name],
        ['Phí thanh toán', selectedPkg.price === 0 ? 'Miễn phí' : formatVND(selectedPkg.price)],
        ['Thời hạn hiển thị', `${selectedPkg.durationDays} ngày`],
        ['Nhãn quảng cáo', draft.bannerOn ? draft.banner || selectedPkg.badge : 'Tắt'],
      ],
    },
  ] as const

  return (
    <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_288px]">
      <div className="flex flex-col gap-4">
        {sections.map((s) => (
          <section key={s.title} className="rounded-xl border border-border bg-card p-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">{s.title}</h3>
              <button
                type="button"
                onClick={() => onEdit(s.step)}
                className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
              >
                <Pencil className="size-3.5" aria-hidden="true" /> Chỉnh sửa
              </button>
            </div>
            <dl className="mt-2 divide-y divide-border">
              {s.rows.map(([l, v]) => (
                <Row key={l} label={l} value={v} />
              ))}
            </dl>
          </section>
        ))}

        <div className="flex gap-3 rounded-xl bg-brand-soft/60 p-4 text-sm border border-primary/20">
          <ShieldCheck className="size-5 shrink-0 text-primary" aria-hidden="true" />
          <p>
            Tin đăng sẽ được kiểm duyệt bởi đội ngũ quản trị viên trước khi xuất bản rộng rãi. Trạng thái tin sẽ là{' '}
            <strong>Chờ duyệt (PendingModeration)</strong> sau khi hoàn tất.
          </p>
        </div>

        <div>
          <SquareCheck
            label="Tôi cam kết mọi thông tin cung cấp ở trên là hoàn toàn chính xác, trung thực và tôi có quyền hợp pháp đăng tải bất động sản này."
            checked={confirmed}
            onChange={onConfirm}
          />
          {errors.confirm && <p className="mt-1 text-xs text-destructive">{errors.confirm}</p>}
        </div>
      </div>

      <div>
        <PreviewCard draft={draft} />
      </div>
    </div>
  )
}
