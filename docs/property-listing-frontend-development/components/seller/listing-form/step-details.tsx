'use client'

import { CONDITIONS, FACILITIES, PROPERTY_TYPES, VIETNAM_CITIES, VIETNAM_LOCATIONS } from '@/lib/data'
import { cn } from '@/lib/utils'
import { Field, SelectInput, SquareCheck, inputCls, type Draft, type Errors } from './fields'

const counts = ['1', '2', '3', '4', '5', '6']
const carports = ['0', '1', '2', '3', '4']

export function StepDetails({ draft, set, errors }: { draft: Draft; set: (p: Partial<Draft>) => void; errors: Errors }) {
  const currentCity = draft.city || 'TP. Hồ Chí Minh'
  const cityData = VIETNAM_LOCATIONS[currentCity]
  const availableDistricts = cityData ? Object.keys(cityData.districts) : []
  const currentDistrict = draft.district && availableDistricts.includes(draft.district) ? draft.district : availableDistricts[0] || ''
  const availableWards = cityData && currentDistrict ? cityData.districts[currentDistrict] || [] : []

  const text = (key: keyof Draft, placeholder: string, type = 'text') => (
    <input
      id={key}
      type={type}
      inputMode={type === 'number' ? 'numeric' : undefined}
      min={type === 'number' ? 0 : undefined}
      value={draft[key] as string}
      onChange={(e) => set({ [key]: e.target.value })}
      placeholder={placeholder}
      aria-invalid={!!errors[key]}
      aria-describedby={errors[key] ? `${key}-error` : undefined}
      className={cn(inputCls, 'border-border')}
    />
  )

  function toggleFacility(f: string, checked: boolean) {
    if (f === 'None') return set({ facilities: checked ? ['None'] : [] })
    const rest = draft.facilities.filter((x) => x !== 'None' && x !== f)
    set({ facilities: checked ? [...rest, f] : rest })
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Vietnamese Administrative Location Cascading Dropdowns */}
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Tỉnh / Thành phố *" htmlFor="city" error={errors.city}>
          <SelectInput
            id="city"
            value={currentCity}
            onChange={(v) => {
              const newCityData = VIETNAM_LOCATIONS[v]
              const firstDist = newCityData ? Object.keys(newCityData.districts)[0] || '' : ''
              const firstWard = newCityData && firstDist ? newCityData.districts[firstDist]?.[0] || '' : ''
              set({
                city: v,
                district: firstDist,
                suburb: firstDist,
                ward: firstWard,
                council: v,
              })
            }}
            placeholder="Chọn Tỉnh/Thành phố"
            options={VIETNAM_CITIES}
            error={errors.city}
          />
        </Field>
        <Field label="Quận / Huyện *" htmlFor="district" error={errors.district}>
          <SelectInput
            id="district"
            value={currentDistrict}
            onChange={(v) => {
              const wards = cityData?.districts[v] || []
              set({
                district: v,
                suburb: v,
                ward: wards[0] || '',
              })
            }}
            placeholder="Chọn Quận/Huyện"
            options={availableDistricts}
            error={errors.district}
          />
        </Field>
        <Field label="Phường / Xã *" htmlFor="ward" error={errors.ward}>
          <SelectInput
            id="ward"
            value={draft.ward}
            onChange={(v) => set({ ward: v })}
            placeholder="Chọn Phường/Xã"
            options={availableWards}
            error={errors.ward}
          />
        </Field>
      </div>

      <div className="grid gap-3 sm:grid-cols-4">
        <Field label="Số nhà" htmlFor="streetNumber" error={errors.streetNumber} className="sm:col-span-1">
          {text('streetNumber', 'Ví dụ: 42')}
        </Field>
        <Field label="Tên đường / Địa chỉ chi tiết *" htmlFor="address" error={errors.address} className="sm:col-span-3">
          {text('address', 'Ví dụ: Nguyễn Văn Hưởng, Phường Thảo Điền')}
        </Field>
      </div>

      <div className="mt-2 grid gap-3 sm:grid-cols-3">
        <Field label="Loại hình BĐS *" htmlFor="propertyType" error={errors.propertyType}>
          <SelectInput
            id="propertyType"
            value={draft.propertyType}
            onChange={(v) => set({ propertyType: v as Draft['propertyType'] })}
            placeholder="Chọn loại BĐS"
            options={PROPERTY_TYPES}
            error={errors.propertyType}
          />
        </Field>
        <Field label="Số phòng ngủ" htmlFor="beds" error={errors.beds}>
          <SelectInput id="beds" value={draft.beds} onChange={(v) => set({ beds: v })} placeholder="Chọn số phòng" options={counts} error={errors.beds} />
        </Field>
        <Field label="Số phòng tắm / vệ sinh" htmlFor="baths" error={errors.baths}>
          <SelectInput id="baths" value={draft.baths} onChange={(v) => set({ baths: v })} placeholder="Chọn số phòng" options={counts} error={errors.baths} />
        </Field>
        <Field label="Chỗ để xe ô tô" htmlFor="carports" error={errors.carports}>
          <SelectInput id="carports" value={draft.carports} onChange={(v) => set({ carports: v })} placeholder="Số chỗ đỗ xe" options={carports} error={errors.carports} />
        </Field>
        <Field label="Diện tích đất (m²)" htmlFor="landSize" error={errors.landSize}>
          {text('landSize', 'Ví dụ: 150', 'number')}
        </Field>
        <Field label="Tình trạng nhà" htmlFor="condition" error={errors.condition}>
          <SelectInput id="condition" value={draft.condition} onChange={(v) => set({ condition: v })} placeholder="Chọn tình trạng" options={CONDITIONS} error={errors.condition} />
        </Field>
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-2 text-xs text-muted-foreground">Tiện ích BĐS & Khu vực xung quanh</legend>
        <div className="flex flex-wrap gap-x-4 gap-y-2">
          {FACILITIES.map((f) => (
            <SquareCheck key={f} label={f} checked={draft.facilities.includes(f)} onChange={(c) => toggleFacility(f, c)} />
          ))}
        </div>
      </fieldset>

      <Field label="Ghi chú nội bộ cho người đăng" htmlFor="notes">
        {text('notes', 'Ghi chú riêng tư về liên hệ hoặc thủ tục...')}
      </Field>
      <SquareCheck label="Hiển thị ghi chú này trên bài đăng công khai" checked={draft.displayNotes} onChange={(c) => set({ displayNotes: c })} />
    </div>
  )
}
