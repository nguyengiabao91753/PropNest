'use client'

import { cn } from '@/lib/utils'
import { Field, Toggle, inputCls, type Draft, type Errors } from './fields'
import { PreviewCard } from './preview-card'

export function StepTemplate({ draft, set, errors }: { draft: Draft; set: (p: Partial<Draft>) => void; errors: Errors }) {
  return (
    <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_288px]">
      <div className="flex flex-col gap-4">
        <Toggle id="bannerOn" label="Add Banner" checked={draft.bannerOn} onChange={(c) => set({ bannerOn: c })} />
        {draft.bannerOn && (
          <Field label="Banner Name" htmlFor="banner" error={errors.banner}>
            <input
              id="banner"
              maxLength={18}
              value={draft.banner}
              onChange={(e) => set({ banner: e.target.value })}
              placeholder="Type Banner Name"
              aria-invalid={!!errors.banner}
              className={cn(inputCls, 'border-border')}
            />
          </Field>
        )}
        <Toggle id="showAddress" label="Put address" checked={draft.showAddress} onChange={(c) => set({ showAddress: c })} />
      </div>
      <div>
        <PreviewCard draft={draft} />
      </div>
    </div>
  )
}
