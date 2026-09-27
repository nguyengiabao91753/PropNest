'use client'

import { Check, ShieldAlert, Sparkles, Wallet as WalletIcon, Zap } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { PACKAGES, type PackageCode } from '@/lib/data'
import { formatVND } from '@/lib/format'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'
import type { Draft } from './fields'

export function StepPackage({
  draft,
  set,
  simulateFailure,
  setSimulateFailure,
}: {
  draft: Draft
  set: (p: Partial<Draft>) => void
  simulateFailure: boolean
  setSimulateFailure: (f: boolean) => void
}) {
  const { wallet, topUpWallet } = useStore()
  const totalBalance = wallet.mainBalance + wallet.promoBalance

  return (
    <div className="flex flex-col gap-6">
      {/* Wallet Balance Widget */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 rounded-xl border border-primary/30 bg-brand-soft/50 p-4">
        <div className="flex items-center gap-3">
          <div className="grid size-11 place-items-center rounded-lg bg-primary text-primary-foreground shadow-xs">
            <WalletIcon className="size-5" />
          </div>
          <div>
            <p className="text-xs text-muted-foreground uppercase tracking-wider font-medium">Số dư ví của bạn</p>
            <div className="flex items-baseline gap-2">
              <span className="text-xl font-bold font-display text-foreground">{formatVND(totalBalance)}</span>
              <span className="text-xs text-muted-foreground">
                (Chính: {formatVND(wallet.mainBalance)} · KM: {formatVND(wallet.promoBalance)})
              </span>
            </div>
          </div>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="border-primary/50 hover:bg-primary hover:text-primary-foreground text-xs"
          onClick={() => topUpWallet(1_000_000)}
        >
          + Nạp nhanh 1.000.000 ₫ (Demo)
        </Button>
      </div>

      {/* Package Selection Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        {PACKAGES.map((pkg) => {
          const isSelected = draft.packageCode === pkg.code
          const isVip = pkg.code === 'VIP'
          const isBoost = pkg.code === 'Boost'

          return (
            <div
              key={pkg.code}
              onClick={() => set({ packageCode: pkg.code })}
              className={cn(
                'relative flex cursor-pointer flex-col justify-between rounded-2xl border p-5 transition-all',
                isSelected
                  ? 'border-primary ring-2 ring-primary/40 bg-card shadow-md'
                  : 'border-border bg-card/60 hover:border-primary/60 hover:bg-card',
                isVip && isSelected && 'border-amber-500 ring-amber-500/40',
              )}
            >
              {pkg.highlight && (
                <span className="absolute -top-3 right-4 rounded-full bg-amber-500 px-3 py-0.5 text-[11px] font-semibold text-white shadow-xs">
                  {pkg.badge}
                </span>
              )}

              <div>
                <div className="flex items-center justify-between mb-3">
                  <span className="text-sm font-semibold flex items-center gap-1.5">
                    {isVip ? <Sparkles className="size-4 text-amber-500" /> : isBoost ? <Zap className="size-4 text-blue-500" /> : null}
                    {pkg.name}
                  </span>
                  <div
                    className={cn(
                      'grid size-5 place-items-center rounded-full border transition-colors',
                      isSelected ? 'border-primary bg-primary text-primary-foreground' : 'border-border',
                    )}
                  >
                    {isSelected && <Check className="size-3" strokeWidth={3} />}
                  </div>
                </div>

                <div className="mb-3">
                  <span className="text-2xl font-bold font-display">{pkg.price === 0 ? 'Miễn phí' : formatVND(pkg.price)}</span>
                  <span className="text-xs text-muted-foreground ml-1">/ {pkg.durationDays} ngày</span>
                </div>

                <p className="text-xs text-muted-foreground mb-4">{pkg.description}</p>

                <ul className="space-y-2 text-xs">
                  {pkg.features.map((feat, i) => (
                    <li key={i} className="flex items-center gap-2">
                      <Check className="size-3.5 text-emerald-600 shrink-0" />
                      <span>{feat}</span>
                    </li>
                  ))}
                </ul>
              </div>

              <div className="mt-5 pt-4 border-t border-border/60">
                <Button
                  type="button"
                  variant={isSelected ? 'default' : 'outline'}
                  size="sm"
                  className="w-full text-xs"
                >
                  {isSelected ? 'Đã chọn gói này' : 'Chọn gói'}
                </Button>
              </div>
            </div>
          )
        })}
      </div>

      {/* Demo Saga Compensation Simulator */}
      <div className="rounded-xl border border-dashed border-border bg-surface p-4">
        <label className="flex items-start gap-3 cursor-pointer">
          <input
            type="checkbox"
            checked={simulateFailure}
            onChange={(e) => setSimulateFailure(e.target.checked)}
            className="mt-0.5 size-4 rounded border-border text-destructive focus:ring-destructive"
          />
          <div>
            <span className="text-sm font-semibold text-destructive flex items-center gap-1.5">
              <ShieldAlert className="size-4" />
              [Chế độ Demo] Giả lập lỗi khi Nâng cấp gói để chứng minh Saga Compensation (Hoàn tiền)
            </span>
            <p className="text-xs text-muted-foreground mt-0.5">
              Khi bật tùy chọn này, hệ thống sẽ thực hiện trừ tiền ví thành công, nhưng giả lập lỗi ở bước UpgradeListing để kích hoạt kịch bản bồi hoàn: Refund ví và chuyển trạng thái Saga thành Compensated.
            </p>
          </div>
        </label>
      </div>
    </div>
  )
}
