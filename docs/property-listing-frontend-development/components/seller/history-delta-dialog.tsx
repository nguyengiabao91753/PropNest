'use client'

import { Clock, History, User } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDate } from '@/lib/format'
import { useStore } from '@/lib/store'
import type { Property } from '@/lib/data'

export function HistoryDeltaDialog({
  property,
  open,
  onOpenChange,
}: {
  property: Property | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { getHistories } = useStore()
  if (!property) return null

  const histories = getHistories(property.id)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-2 text-primary font-semibold text-sm">
            <History className="size-4" />
            <span>Thuật toán DeltaEngine & Snapshot</span>
          </div>
          <DialogTitle className="font-display text-2xl font-bold">
            Lịch sử thay đổi: {property.id}
          </DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            {property.title} — Lưu trữ vết kiểm toán (Audit Trail) và độ lệch trường thông tin (Delta Changes).
          </DialogDescription>
        </DialogHeader>

        <div className="mt-4 space-y-6">
          {histories.length === 0 ? (
            <p className="text-center py-8 text-sm text-muted-foreground">Chưa có bản ghi lịch sử thay đổi nào cho tin này.</p>
          ) : (
            <div className="relative border-l-2 border-border/80 pl-6 ml-3 space-y-6">
              {histories.map((h, i) => (
                <div key={h.historyId} className="relative group">
                  {/* Timeline bullet */}
                  <div className="absolute -left-[31px] top-1.5 size-3 rounded-full border-2 border-primary bg-background" />

                  <div className="flex flex-col gap-2 rounded-xl border border-border bg-card p-4 shadow-2xs">
                    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 pb-2">
                      <div className="flex items-center gap-2">
                        <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">
                          {h.actionType}
                        </span>
                        <span className="text-xs font-medium text-foreground flex items-center gap-1">
                          <User className="size-3 text-muted-foreground" /> {h.actor}
                        </span>
                      </div>
                      <span className="text-[11px] text-muted-foreground flex items-center gap-1 font-mono">
                        <Clock className="size-3" /> {formatDate(h.actionDate)}
                      </span>
                    </div>

                    {h.note && <p className="text-xs text-muted-foreground italic">{h.note}</p>}

                    {/* Delta changes table */}
                    {h.deltaChanges && h.deltaChanges.length > 0 && (
                      <div className="mt-1 overflow-x-auto rounded-lg border border-border/60 bg-surface text-xs">
                        <table className="w-full text-left">
                          <thead className="border-b border-border/60 text-muted-foreground bg-muted/40">
                            <tr>
                              <th className="px-3 py-1.5 font-medium">Trường (Field)</th>
                              <th className="px-3 py-1.5 font-medium text-rose-600">Giá trị trước</th>
                              <th className="px-3 py-1.5 font-medium text-emerald-600">Giá trị sau (New)</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-border/40 font-mono">
                            {h.deltaChanges.map((d, idx) => (
                              <tr key={idx} className="hover:bg-muted/20">
                                <td className="px-3 py-1.5 font-semibold text-foreground">{d.field}</td>
                                <td className="px-3 py-1.5 text-muted-foreground">
                                  {d.oldValue === null ? <em className="text-muted-foreground/60">null</em> : String(d.oldValue)}
                                </td>
                                <td className="px-3 py-1.5 text-emerald-600 dark:text-emerald-400 font-semibold">
                                  {String(d.newValue)}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}
