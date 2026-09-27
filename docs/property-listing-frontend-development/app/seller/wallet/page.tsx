'use client'

import { useState } from 'react'
import { ArrowDownLeft, ArrowUpRight, CheckCircle2, CreditCard, History, Plus, RefreshCcw, ShieldCheck, Wallet as WalletIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDate, formatVND } from '@/lib/format'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

export default function SellerWalletPage() {
  const { wallet, transactions, topUpWallet } = useStore()
  const [topUpOpen, setTopUpOpen] = useState(false)
  const [amount, setAmount] = useState(1_000_000)

  const totalBalance = wallet.mainBalance + wallet.promoBalance

  const handleTopUp = () => {
    topUpWallet(amount)
    toast.success(`Nạp thành công ${formatVND(amount)} vào số dư chính!`)
    setTopUpOpen(false)
  }

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 md:px-8">
      <div className="flex flex-wrap items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="font-display text-3xl font-bold">Ví Tài Khoản & Giao Dịch</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Quản lý số dư, theo dõi lịch sử thanh toán mua gói VIP và kiểm toán bồi hoàn (Saga Refund).
          </p>
        </div>
        <Button onClick={() => setTopUpOpen(true)} className="gap-2 bg-primary text-primary-foreground font-semibold">
          <Plus className="size-4" /> Nạp tiền vào ví
        </Button>
      </div>

      {/* Balance Cards */}
      <div className="grid gap-6 md:grid-cols-3 mb-8">
        <div className="rounded-2xl border border-primary/30 bg-brand-soft/60 p-6 shadow-xs relative overflow-hidden">
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Tổng số dư khả dụng</span>
            <div className="grid size-10 place-items-center rounded-xl bg-primary text-primary-foreground">
              <WalletIcon className="size-5" />
            </div>
          </div>
          <p className="font-display text-3xl font-extrabold text-foreground">{formatVND(totalBalance)}</p>
          <p className="text-xs text-muted-foreground mt-2">Dùng để thanh toán gói VIP và đẩy tin tự động</p>
        </div>

        <div className="rounded-2xl border border-border bg-card p-6 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Số dư chính (Main Balance)</span>
            <div className="grid size-10 place-items-center rounded-xl bg-emerald-500/15 text-emerald-600">
              <CreditCard className="size-5" />
            </div>
          </div>
          <p className="font-display text-3xl font-bold text-foreground">{formatVND(wallet.mainBalance)}</p>
          <p className="text-xs text-muted-foreground mt-2">Nạp từ thẻ ngân hàng, tiền mặt hoặc chuyển khoản</p>
        </div>

        <div className="rounded-2xl border border-border bg-card p-6 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Số dư khuyến mãi (Promo)</span>
            <div className="grid size-10 place-items-center rounded-xl bg-amber-500/15 text-amber-600">
              <ShieldCheck className="size-5" />
            </div>
          </div>
          <p className="font-display text-3xl font-bold text-foreground">{formatVND(wallet.promoBalance)}</p>
          <p className="text-xs text-muted-foreground mt-2">Ưu tiên trừ trước khi thanh toán gói tin</p>
        </div>
      </div>

      {/* Optimistic Concurrency & Policy Info */}
      <div className="rounded-xl border border-border bg-surface/80 p-4 mb-8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 text-xs text-muted-foreground">
        <div className="flex items-center gap-2">
          <span className="font-semibold text-foreground">Optimistic Concurrency:</span>
          <code className="rounded bg-muted px-2 py-0.5 font-mono text-[11px]">{wallet.rowVersion}</code>
        </div>
        <div className="flex items-center gap-2">
          <span className="font-semibold text-foreground">Quy tắc trừ tiền:</span>
          <span>Khuyến mãi (Promo) trừ trước → Số dư chính (Main) trừ sau → Hoàn tiền bồi hoàn đúng số tiền đã trừ.</span>
        </div>
      </div>

      {/* Transaction Ledger Table */}
      <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-xs">
        <div className="border-b border-border px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <History className="size-4 text-primary" />
            <h2 className="text-base font-semibold text-foreground">Sổ cái giao dịch (Immutable Ledger)</h2>
          </div>
          <span className="text-xs text-muted-foreground">{transactions.length} giao dịch đã ghi nhận</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border text-xs text-muted-foreground bg-surface/50 uppercase">
              <tr>
                <th className="px-6 py-3 font-medium">Thời gian</th>
                <th className="px-6 py-3 font-medium">Loại</th>
                <th className="px-6 py-3 font-medium">Correlation ID</th>
                <th className="px-6 py-3 font-medium">Nội dung</th>
                <th className="px-6 py-3 font-medium text-right">Số tiền</th>
                <th className="px-6 py-3 font-medium text-right">Số dư sau GD</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {transactions.map((tx) => {
                const isDeposit = tx.type === 'Deposit'
                const isCharge = tx.type === 'Charge'
                const isRefund = tx.type === 'Refund'

                return (
                  <tr key={tx.id} className="transition-colors hover:bg-surface/60">
                    <td className="px-6 py-4 whitespace-nowrap text-xs text-muted-foreground">
                      {formatDate(tx.createdAt)}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span
                        className={cn(
                          'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold',
                          isDeposit && 'bg-emerald-500/15 text-emerald-600 border border-emerald-500/30',
                          isCharge && 'bg-blue-500/15 text-blue-600 border border-blue-500/30',
                          isRefund && 'bg-amber-500/15 text-amber-600 border border-amber-500/30',
                        )}
                      >
                        {isDeposit && <ArrowDownLeft className="size-3" />}
                        {isCharge && <ArrowUpRight className="size-3" />}
                        {isRefund && <RefreshCcw className="size-3" />}
                        {isDeposit ? 'Nạp tiền' : isCharge ? 'Thanh toán' : 'Hoàn tiền Saga'}
                      </span>
                    </td>
                    <td className="px-6 py-4 font-mono text-xs text-muted-foreground max-w-44 truncate" title={tx.correlationId}>
                      {tx.correlationId}
                    </td>
                    <td className="px-6 py-4 text-foreground font-medium">{tx.description}</td>
                    <td
                      className={cn(
                        'px-6 py-4 text-right font-semibold whitespace-nowrap',
                        isDeposit && 'text-emerald-600',
                        isCharge && 'text-rose-600',
                        isRefund && 'text-amber-600',
                      )}
                    >
                      {isCharge ? `- ${formatVND(tx.amount)}` : `+ ${formatVND(tx.amount)}`}
                    </td>
                    <td className="px-6 py-4 text-right font-mono text-xs text-muted-foreground whitespace-nowrap">
                      {formatVND(tx.balanceAfter)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Top up modal */}
      <Dialog open={topUpOpen} onOpenChange={setTopUpOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Nạp tiền vào tài khoản demo</DialogTitle>
            <DialogDescription>
              Số tiền nạp sẽ được cộng trực tiếp vào Số dư chính của ví để bạn trải nghiệm luồng mua gói tin VIP.
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-3 gap-3 py-4">
            {[500_000, 1_000_000, 2_000_000].map((v) => (
              <button
                key={v}
                type="button"
                onClick={() => setAmount(v)}
                className={cn(
                  'rounded-xl border p-3 text-center text-sm font-semibold transition-all',
                  amount === v ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-card hover:border-primary',
                )}
              >
                {formatVND(v)}
              </button>
            ))}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setTopUpOpen(false)}>
              Hủy
            </Button>
            <Button onClick={handleTopUp} className="bg-primary text-primary-foreground font-semibold">
              Xác nhận nạp {formatVND(amount)}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
