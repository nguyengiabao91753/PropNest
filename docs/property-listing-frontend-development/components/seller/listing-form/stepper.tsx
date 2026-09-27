import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'

export const STEPS = [
  'Thông tin BĐS',
  'Hình ảnh & Giá',
  'Giao diện hiển thị',
  'Chọn gói dịch vụ',
  'Xác nhận & Đăng',
] as const

export function Stepper({ current, onJump }: { current: number; onJump: (i: number) => void }) {
  return (
    <ol className="flex flex-wrap items-center justify-center gap-x-6 gap-y-4">
      {STEPS.map((label, i) => {
        const done = i < current
        const active = i === current
        return (
          <li key={label}>
            <button
              type="button"
              disabled={!done}
              onClick={() => onJump(i)}
              aria-current={active ? 'step' : undefined}
              className="flex items-center gap-2.5 disabled:cursor-default"
            >
              <span
                className={cn(
                  'grid size-10 place-items-center rounded-full text-sm font-semibold transition-colors',
                  done && 'bg-brand-soft text-foreground',
                  active && 'bg-foreground text-background',
                  !done && !active && 'bg-card text-muted-foreground border border-border',
                )}
              >
                {done ? <Check className="size-4" aria-label="Completed" /> : String(i + 1).padStart(2, '0')}
              </span>
              <span className={cn('hidden text-sm sm:inline', (done || active) && 'font-semibold text-foreground')}>
                {label}
              </span>
            </button>
          </li>
        )
      })}
    </ol>
  )
}
