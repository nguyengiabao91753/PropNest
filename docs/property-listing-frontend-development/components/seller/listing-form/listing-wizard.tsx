'use client'

import { useState } from 'react'
import { ArrowLeft, ArrowRight, Check, FileText, RefreshCw, Send, ShieldCheck, Sparkles, XCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useStore } from '@/lib/store'
import { CURRENT_SELLER, PACKAGES, type Property } from '@/lib/data'
import { emptyDraft, type Draft } from './fields'
import { Stepper, STEPS } from './stepper'
import { StepDetails } from './step-details'
import { StepMedia } from './step-media'
import { StepTemplate } from './step-template'
import { StepPackage } from './step-package'
import { StepSubmit } from './step-submit'

export function ListingWizard({ editId }: { editId?: string }) {
  const { properties, addProperty, updateProperty, startPurchaseWorkflow } = useStore()
  const existing = editId ? properties.find((p) => p.id === editId) : undefined
  const [step, setStep] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [submittedResult, setSubmittedResult] = useState<{
    success: boolean
    correlationId?: string
    listingId: string
    message: string
    isDraft?: boolean
  } | null>(null)

  const [simulateFailure, setSimulateFailure] = useState(false)

  const [draft, setDraft] = useState<Draft>(() =>
    existing
      ? {
          ...emptyDraft,
          ...existing,
          beds: String(existing.beds),
          baths: String(existing.baths),
          carports: String(existing.carports),
          landSize: String(existing.landSize),
          sqft: String(existing.sqft || existing.area),
          area: String(existing.area || existing.sqft),
          price: String(existing.price),
          bannerOn: !!existing.banner,
          banner: existing.banner ?? '',
          packageCode: existing.packageCode ?? 'VIP',
        }
      : emptyDraft,
  )

  const patch = (next: Partial<Draft>) => setDraft((d) => ({ ...d, ...next }))
  const errors = {} as Record<string, string>

  // Save as Draft (F02)
  const handleSaveDraft = () => {
    const listingId = existing?.id ?? `PN-${Math.floor(1000 + Math.random() * 9000)}`
    const base: Property = {
      ...(existing ?? properties[0]),
      ...draft,
      id: listingId,
      title: draft.title || `${draft.propertyType} tại ${draft.district || 'TP. Hồ Chí Minh'}`,
      price: Number(draft.price) || 0,
      beds: Number(draft.beds) || 0,
      baths: Number(draft.baths) || 0,
      carports: Number(draft.carports) || 0,
      area: Number(draft.area || draft.sqft) || 100,
      sqft: Number(draft.area || draft.sqft) || 100,
      landSize: Number(draft.landSize) || 120,
      images: draft.images.length ? draft.images : properties[0].images,
      sellerId: CURRENT_SELLER.id,
      sellerName: CURRENT_SELLER.name,
      sellerAvatar: CURRENT_SELLER.avatar,
      status: 'Draft',
      packageCode: draft.packageCode,
      createdAt: new Date().toISOString(),
      rowVersion: `AAAAAA${Date.now().toString().slice(-4)}A=`,
      featured: false,
      views: 0,
      inquiries: 0,
      rating: 4.8,
      reviews: 0,
      city: draft.city || 'TP. Hồ Chí Minh',
      district: draft.district || 'Quận 2 (Thủ Đức)',
      ward: draft.ward || 'Thảo Điền',
      suburb: draft.district || 'Quận 2 (Thủ Đức)',
      council: draft.city || 'TP. Hồ Chí Minh',
      category: draft.propertyType === 'Commercial' ? 'Commercial' : draft.propertyType === 'Apartment' ? 'Apartments' : 'Residential',
      banner: draft.bannerOn ? draft.banner : undefined,
    }

    if (existing) updateProperty(existing.id, base, 'Lưu bản nháp tin đăng')
    else addProperty(base)

    setSubmittedResult({
      success: true,
      listingId,
      message: 'Tin đăng đã được lưu vào danh sách Bản nháp (Draft). Bạn có thể tiếp tục chỉnh sửa bất cứ lúc nào.',
      isDraft: true,
    })
  }

  // Submit and initiate Saga purchase workflow (F06)
  const handleSubmitAndPay = async () => {
    setSubmitting(true)
    const listingId = existing?.id ?? `PN-${Math.floor(1000 + Math.random() * 9000)}`

    const base: Property = {
      ...(existing ?? properties[0]),
      ...draft,
      id: listingId,
      title: draft.title || `${draft.propertyType} tại ${draft.district || 'TP. Hồ Chí Minh'}`,
      price: Number(draft.price) || 0,
      beds: Number(draft.beds) || 0,
      baths: Number(draft.baths) || 0,
      carports: Number(draft.carports) || 0,
      area: Number(draft.area || draft.sqft) || 100,
      sqft: Number(draft.area || draft.sqft) || 100,
      landSize: Number(draft.landSize) || 120,
      images: draft.images.length ? draft.images : properties[0].images,
      sellerId: CURRENT_SELLER.id,
      sellerName: CURRENT_SELLER.name,
      sellerAvatar: CURRENT_SELLER.avatar,
      status: 'PendingPayment',
      packageCode: draft.packageCode,
      createdAt: new Date().toISOString(),
      rowVersion: `AAAAAA${Date.now().toString().slice(-4)}A=`,
      featured: draft.packageCode === 'VIP',
      banner: draft.packageCode === 'VIP' ? 'VIP Nổi Bật' : draft.packageCode === 'Boost' ? 'Hot Listing' : undefined,
      views: 0,
      inquiries: 0,
      rating: 4.8,
      reviews: 0,
      city: draft.city || 'TP. Hồ Chí Minh',
      district: draft.district || 'Quận 2 (Thủ Đức)',
      ward: draft.ward || 'Thảo Điền',
      suburb: draft.district || 'Quận 2 (Thủ Đức)',
      council: draft.city || 'TP. Hồ Chí Minh',
      category: draft.propertyType === 'Commercial' ? 'Commercial' : draft.propertyType === 'Apartment' ? 'Apartments' : 'Residential',
    }

    if (existing) updateProperty(existing.id, base)
    else addProperty(base)

    try {
      // Execute Saga workflow: Deduct Wallet -> Upgrade Listing -> Record History
      const correlationId = await startPurchaseWorkflow(listingId, draft.packageCode, simulateFailure)
      setSubmittedResult({
        success: true,
        correlationId,
        listingId,
        message:
          draft.packageCode === 'Standard'
            ? 'Tin đăng miễn phí đã gửi vào hàng đợi kiểm duyệt thành công!'
            : `Thanh toán ${draft.packageCode} thành công qua MassTransit Saga! Tin đăng của bạn đã được xuất bản và ghim nổi bật.`,
      })
    } catch (err: any) {
      setSubmittedResult({
        success: false,
        listingId,
        message: err?.message || 'Có lỗi xảy ra trong quá trình thực thi Saga Orchestrator.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  // Success / Failure Screen with Saga Evidence
  if (submittedResult) {
    const isSuccess = submittedResult.success
    const pkg = PACKAGES.find((p) => p.code === draft.packageCode)

    return (
      <div className="mx-auto max-w-2xl rounded-2xl border bg-card p-8 md:p-12 text-center shadow-md">
        <div
          className={`mx-auto mb-5 grid size-16 place-items-center rounded-full ${
            isSuccess ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'
          }`}
        >
          {isSuccess ? <Check className="size-8" /> : <XCircle className="size-8" />}
        </div>

        <h1 className="font-display text-3xl font-bold">
          {submittedResult.isDraft
            ? 'Bản nháp đã lưu thành công'
            : isSuccess
              ? 'Thực thi Saga Hoàn tất'
              : 'Giao dịch Thất bại & Đã Bồi hoàn (Compensated)'}
        </h1>

        <p className="mt-3 text-sm text-muted-foreground max-w-lg mx-auto">{submittedResult.message}</p>

        {submittedResult.correlationId && (
          <div className="mt-6 rounded-xl border border-primary/30 bg-brand-soft/60 p-4 text-left text-xs font-mono">
            <div className="flex justify-between items-center mb-1 text-muted-foreground">
              <span className="font-sans font-semibold text-foreground">Bằng chứng Saga Orchestrator</span>
              <span className="rounded bg-primary/20 px-2 py-0.5 text-primary text-[10px]">MassTransit Outbox</span>
            </div>
            <p className="truncate">
              <strong>CorrelationId:</strong> {submittedResult.correlationId}
            </p>
            <p>
              <strong>ListingId:</strong> {submittedResult.listingId}
            </p>
            <p>
              <strong>Gói dịch vụ:</strong> {pkg?.name} ({pkg?.price.toLocaleString('vi-VN')} ₫)
            </p>
            <p>
              <strong>Trạng thái Saga:</strong> {isSuccess ? 'Completed (Đã xác nhận)' : 'Compensated (Đã hoàn tiền)'}
            </p>
          </div>
        )}

        <div className="mt-8 flex justify-center gap-3">
          <Button
            variant="outline"
            onClick={() => {
              setSubmittedResult(null)
              setStep(0)
            }}
          >
            Tạo tin khác
          </Button>
          <Button onClick={() => (window.location.href = '/seller/properties')}>Xem danh sách tin của tôi</Button>
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-6">
      <Stepper current={step} onJump={setStep} />

      <div className="mt-10 grid gap-10 lg:grid-cols-[1fr_280px]">
        <div>
          <div className="mb-8">
            <h1 className="font-display text-3xl font-bold">{STEPS[step]}</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              {step === 0 && 'Điền thông tin địa giới hành chính Việt Nam và quy cách bất động sản.'}
              {step === 1 && 'Tải ảnh đẹp và thiết lập mức giá niêm yết cạnh tranh.'}
              {step === 2 && 'Cấu hình nhãn quảng cáo và hiển thị trên giao diện tìm kiếm.'}
              {step === 3 && 'Lựa chọn gói tin (Standard, VIP, Boost) và kiểm tra số dư ví thanh toán.'}
              {step === 4 && 'Rà soát thông tin trước khi lưu nháp hoặc thanh toán xuất bản.'}
            </p>
          </div>

          {step === 0 && <StepDetails draft={draft} set={patch} errors={errors} />}
          {step === 1 && <StepMedia draft={draft} set={patch} errors={errors} />}
          {step === 2 && <StepTemplate draft={draft} set={patch} errors={errors} />}
          {step === 3 && (
            <StepPackage
              draft={draft}
              set={patch}
              simulateFailure={simulateFailure}
              setSimulateFailure={setSimulateFailure}
            />
          )}
          {step === 4 && (
            <StepSubmit draft={draft} confirmed={true} onConfirm={() => {}} errors={errors} onEdit={setStep} />
          )}

          <div className="mt-10 flex flex-wrap items-center justify-between gap-3 border-t border-border pt-6">
            <div>
              {step > 0 && (
                <Button variant="outline" onClick={() => setStep(step - 1)}>
                  <ArrowLeft className="size-4" /> Quay lại
                </Button>
              )}
            </div>

            <div className="flex items-center gap-3">
              <Button type="button" variant="outline" onClick={handleSaveDraft} className="border-border">
                <FileText className="size-4" /> Lưu bản nháp (Draft)
              </Button>

              {step < 4 ? (
                <Button onClick={() => setStep(step + 1)}>
                  Tiếp theo <ArrowRight className="size-4" />
                </Button>
              ) : (
                <Button onClick={handleSubmitAndPay} disabled={submitting} className="bg-primary text-primary-foreground font-semibold">
                  {submitting ? (
                    <>
                      <RefreshCw className="size-4 animate-spin" /> Đang điều phối Saga...
                    </>
                  ) : (
                    <>
                      <Sparkles className="size-4" /> Thanh toán & Xuất bản tin
                    </>
                  )}
                </Button>
              )}
            </div>
          </div>
        </div>

        {step > 0 && (
          <div className="hidden lg:block">
            <div className="sticky top-8">
              <p className="mb-3 text-xs uppercase tracking-wider text-muted-foreground font-medium">Xem trước thẻ tin</p>
              <div className="rounded-2xl border border-border bg-card p-3 shadow-xs">
                <img
                  src={draft.images[0] || '/images/p1.png'}
                  alt="Listing preview"
                  className="aspect-[4/3] w-full rounded-xl object-cover"
                />
                <div className="p-3">
                  <p className="font-display text-xl font-bold text-foreground">
                    {draft.price
                      ? `${(Number(draft.price) / 1_000_000_000 >= 1
                          ? `${(Number(draft.price) / 1_000_000_000).toFixed(1)} tỷ`
                          : `${Math.round(Number(draft.price) / 1_000_000)} triệu`)} ₫`
                      : '0 ₫'}
                  </p>
                  <p className="mt-1 text-sm line-clamp-1 font-medium">{draft.title || 'Tiêu đề bất động sản'}</p>
                  <p className="mt-2 text-xs text-muted-foreground">
                    {draft.beds || 0} PN · {draft.baths || 0} WC · {draft.sqft || draft.area || 0} m²
                  </p>
                  <p className="mt-1 text-xs text-muted-foreground line-clamp-1">
                    {[draft.ward, draft.district, draft.city].filter(Boolean).join(', ')}
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
