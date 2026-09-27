'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { ArrowLeft, Home, LogOut, MessagesSquare, PlusCircle, UserRound, Wallet } from 'lucide-react'
import { Logo } from '@/components/shared/brand'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { CURRENT_SELLER } from '@/lib/data'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

const links = [
  {
    href: '/seller/properties',
    label: 'Bất động sản của tôi',
    icon: Home,
    match: (p: string) => p === '/seller/properties' || /^\/seller\/properties\/(?!new)/.test(p),
  },
  {
    href: '/seller/properties/new',
    label: 'Đăng tin mới',
    icon: PlusCircle,
    match: (p: string) => p === '/seller/properties/new',
  },
  {
    href: '/seller/wallet',
    label: 'Ví & Giao dịch',
    icon: Wallet,
    match: (p: string) => p === '/seller/wallet',
  },
]

export function SellerHeader() {
  const pathname = usePathname()
  const router = useRouter()
  const { session, signOut, properties } = useStore()

  const rejectedCount = properties.filter(
    (p) => p.sellerId === CURRENT_SELLER.id && (p.status === 'Rejected' || (p.status as any) === 'rejected'),
  ).length

  return (
    <header className="fixed inset-y-0 left-0 z-40 hidden w-64 border-r border-border bg-surface lg:block">
      <div className="flex h-full flex-col gap-6 px-5 py-6">
        <div className="flex items-center justify-between">
          <Logo href="/seller/properties" />
          <span className="rounded-full bg-primary/20 text-primary border border-primary/30 px-2.5 py-0.5 text-[11px] font-bold">
            Người bán
          </span>
        </div>

        <nav className="flex flex-1 flex-col gap-1.5" aria-label="Cổng Người bán">
          {links.map((l) => {
            const active = l.match(pathname)
            const Icon = l.icon

            return (
              <Link
                key={l.href}
                href={l.href}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-3 rounded-xl px-3.5 py-2.5 text-sm font-medium transition-colors',
                  active
                    ? 'bg-brand-soft text-primary font-semibold'
                    : 'text-muted-foreground hover:bg-card hover:text-foreground',
                )}
              >
                <Icon className="size-4 shrink-0" />
                {l.label}
              </Link>
            )
          })}
        </nav>

        {/* Rejected notices alert for seller */}
        <Link
          href="/seller/properties?status=Rejected"
          className="flex items-center justify-between rounded-xl border border-border bg-card p-3 text-xs text-muted-foreground hover:border-primary/50 transition-colors"
        >
          <div className="flex items-center gap-2">
            <MessagesSquare className="size-4 text-foreground" />
            <span>Phản hồi kiểm duyệt</span>
          </div>
          {rejectedCount > 0 && (
            <span className="grid size-5 place-items-center rounded-full bg-destructive text-[10px] font-bold text-white">
              {rejectedCount}
            </span>
          )}
        </Link>

        {/* Back to Client Site */}
        <Link
          href="/"
          className="flex items-center gap-2 rounded-xl border border-border px-3.5 py-2 text-xs text-muted-foreground hover:bg-card hover:text-foreground transition-colors"
        >
          <ArrowLeft className="size-3.5" /> Xem trang khách hàng
        </Link>

        {/* User Account */}
        <div className="border-t border-border pt-4">
          <DropdownMenu>
            <DropdownMenuTrigger
              className="flex w-full items-center justify-between rounded-xl border border-border bg-card p-2.5 text-left outline-none hover:border-primary transition-colors"
              aria-label="Menu tài khoản người bán"
            >
              <div className="flex items-center gap-2.5 overflow-hidden">
                <div className="grid size-8 shrink-0 place-items-center rounded-full bg-primary/10 text-primary">
                  <UserRound className="size-4" />
                </div>
                <div className="truncate">
                  <p className="truncate text-xs font-semibold text-foreground">{session?.name ?? CURRENT_SELLER.name}</p>
                  <p className="text-[10px] text-muted-foreground">Tài khoản Người bán</p>
                </div>
              </div>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuGroup>
                <DropdownMenuLabel>Tài khoản Người bán</DropdownMenuLabel>
                <DropdownMenuItem onClick={() => router.push('/seller/wallet')}>
                  <Wallet className="size-4 mr-2" /> Ví & Giao dịch
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => router.push('/seller/properties/new')}>
                  <PlusCircle className="size-4 mr-2" /> Tạo tin mới
                </DropdownMenuItem>
              </DropdownMenuGroup>
              <DropdownMenuSeparator />
              <DropdownMenuGroup>
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() => {
                    signOut()
                    router.push('/login?role=seller')
                  }}
                >
                  <LogOut className="size-4 mr-2" /> Đăng xuất
                </DropdownMenuItem>
              </DropdownMenuGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  )
}
