'use client'

import dynamic from 'next/dynamic'
import Image from 'next/image'
import { Bath, BedDouble, Calendar, Car, Check, Home, Ruler, Sparkles, Trees } from 'lucide-react'
import { toast } from 'sonner'
import type { Property } from '@/lib/data'
import { formatDate, formatNumber, formatPrice } from '@/lib/format'

const LocationMap = dynamic(() => import('./location-map'), {
  ssr: false,
  loading: () => <div className="size-full animate-pulse bg-muted" />,
})

function Spec({ Icon, label, value }: { Icon: typeof Home; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-border bg-card p-4 shadow-xs">
      <span className="grid size-10 place-items-center rounded-full bg-brand-soft text-primary shrink-0">
        <Icon className="size-5" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <dt className="text-xs text-muted-foreground truncate">{label}</dt>
        <dd className="font-semibold text-foreground text-sm truncate">{value}</dd>
      </div>
    </div>
  )
}

function InquiryForm({ property, disabled }: { property: Property; disabled?: boolean }) {
  function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = e.currentTarget
    toast.success('Đã gửi yêu cầu liên hệ!', {
      description: `${property.sellerName} sẽ nhận được tin nhắn và phản hồi cho bạn sớm nhất.`,
    })
    form.reset()
  }

  const field =
    'h-11 w-full rounded-lg border border-border bg-card px-3 text-sm outline-none focus:border-primary disabled:opacity-60 transition-colors'

  return (
    <form id="inquiry" onSubmit={submit} className="scroll-mt-24 rounded-2xl border border-border bg-surface p-6 shadow-xs">
      <div className="flex items-center gap-3">
        <div className="relative size-12 overflow-hidden rounded-full border border-border shrink-0">
          <Image src={property.sellerAvatar || '/images/avatar-2.png'} alt="" fill sizes="48px" className="object-cover" />
        </div>
        <div>
          <p className="font-semibold text-foreground">{property.sellerName}</p>
          <p className="text-xs text-muted-foreground">Người đăng tin (Chính chủ/Môi giới)</p>
        </div>
      </div>

      <fieldset disabled={disabled} className="mt-5 flex flex-col gap-3">
        <legend className="sr-only">Gửi thông tin liên hệ</legend>
        <label className="flex flex-col gap-1.5 text-xs font-medium text-foreground">
          Họ và tên của bạn *
          <input required name="name" className={field} placeholder="Nhập họ và tên" />
        </label>
        <label className="flex flex-col gap-1.5 text-xs font-medium text-foreground">
          Số điện thoại / Email *
          <input required name="contact" className={field} placeholder="0908xxxxxx hoặc you@example.com" />
        </label>
        <label className="flex flex-col gap-1.5 text-xs font-medium text-foreground">
          Lời nhắn *
          <textarea
            required
            name="message"
            rows={3}
            defaultValue={`Chào bạn, tôi quan tâm đến bất động sản "${property.title}". Bạn có thể tư vấn và đặt lịch xem nhà giúp tôi được không?`}
            className="w-full rounded-lg border border-border bg-card p-3 text-sm text-foreground outline-none focus:border-primary disabled:opacity-60"
          />
        </label>
        <button
          type="submit"
          className="h-11 rounded-full bg-primary text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-60 shadow-xs"
        >
          {disabled ? 'Chế độ xem trước' : 'Gửi yêu cầu liên hệ xem nhà'}
        </button>
      </fieldset>
    </form>
  )
}

export function DetailBody({ property: p, previewOnly }: { property: Property; previewOnly?: boolean }) {
  const facilities = p.facilities.filter((f) => f !== 'None')

  return (
    <div id="details" className="mx-auto grid max-w-7xl scroll-mt-20 gap-10 px-4 py-14 md:px-8 lg:grid-cols-[minmax(0,1fr)_360px]">
      <div className="flex flex-col gap-12">
        <section aria-labelledby="overview">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <h2 id="overview" className="font-display text-3xl font-bold">
                Tổng quan <em>bất động sản</em>
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                {p.showAddress
                  ? [p.address, p.ward, p.district, p.city].filter(Boolean).join(', ')
                  : [p.ward, p.district, p.city].filter(Boolean).join(', ')}
              </p>
            </div>
            <p className="font-display text-3xl font-bold text-primary">{formatPrice(p)}</p>
          </div>
          <dl className="mt-6 grid grid-cols-2 gap-3 md:grid-cols-4">
            <Spec Icon={BedDouble} label="Phòng ngủ" value={`${p.beds} PN`} />
            <Spec Icon={Bath} label="Phòng tắm" value={`${p.baths} WC`} />
            <Spec Icon={Car} label="Chỗ đỗ xe" value={`${p.carports} chỗ`} />
            <Spec Icon={Ruler} label="Diện tích sử dụng" value={`${formatNumber(p.area || p.sqft)} m²`} />
            <Spec Icon={Trees} label="Diện tích đất" value={`${formatNumber(p.landSize)} m²`} />
            <Spec Icon={Home} label="Loại hình BĐS" value={p.propertyType} />
            <Spec Icon={Sparkles} label="Tình trạng" value={p.condition} />
            <Spec Icon={Calendar} label="Ngày đăng" value={formatDate(p.createdAt)} />
          </dl>
        </section>

        <section aria-labelledby="desc">
          <h2 id="desc" className="font-display text-2xl font-bold">
            Mô tả chi tiết
          </h2>
          <p className="mt-3 leading-relaxed text-muted-foreground text-pretty text-sm">{p.description}</p>
          {p.displayNotes && p.notes && (
            <p className="mt-4 rounded-xl border-l-4 border-primary bg-brand-soft/50 p-4 text-sm text-foreground">{p.notes}</p>
          )}
        </section>

        {facilities.length > 0 && (
          <section aria-labelledby="facilities">
            <h2 id="facilities" className="font-display text-2xl font-bold">
              Tiện ích nổi bật
            </h2>
            <ul className="mt-4 flex flex-wrap gap-2">
              {facilities.map((f) => (
                <li key={f} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3.5 py-1.5 text-xs font-medium">
                  <Check className="size-3.5 text-primary" aria-hidden="true" />
                  {f}
                </li>
              ))}
            </ul>
          </section>
        )}

        <section aria-labelledby="gallery">
          <h2 id="gallery" className="font-display text-2xl font-bold">
            Hình ảnh thực tế
          </h2>
          <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-3">
            {p.images.map((src, i) => (
              <div
                key={src + i}
                className={
                  i === 0
                    ? 'relative col-span-2 row-span-2 aspect-square overflow-hidden rounded-2xl md:aspect-auto border border-border'
                    : 'relative aspect-square overflow-hidden rounded-2xl border border-border'
                }
              >
                <Image src={src} alt={`${p.title} ảnh ${i + 1}`} fill sizes="(min-width: 768px) 30vw, 50vw" className="object-cover" />
              </div>
            ))}
          </div>
        </section>

        <section aria-labelledby="location">
          <h2 id="location" className="font-display text-2xl font-bold">
            Vị trí bất động sản
          </h2>
          <div className="mt-4 h-72 overflow-hidden rounded-2xl border border-border">
            <LocationMap lat={p.lat} lng={p.lng} />
          </div>
        </section>
      </div>

      <aside className="lg:sticky lg:top-24 lg:self-start">
        <InquiryForm property={p} disabled={previewOnly} />
      </aside>
    </div>
  )
}
