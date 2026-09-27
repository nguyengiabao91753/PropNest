'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { ArrowLeft, CheckSquare, Home, LayoutDashboard, ListFilter, LogOut, ShieldCheck } from 'lucide-react'
import { Logo } from '@/components/shared/brand'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const router = useRouter()
  const { properties, signOut } = useStore()

  const pendingCount = properties.filter(
    (p) => p.status === 'PendingModeration' || (p.status as any) === 'pending',
  ).length

  const navLinks = [
    {
      href: '/admin',
      label: 'Bảng điều khiển',
      icon: LayoutDashboard,
      match: (p: string) => p === '/admin',
    },
    {
      href: '/admin/approve',
      label: 'Duyệt tin đăng',
      icon: CheckSquare,
      badge: pendingCount,
      match: (p: string) => p === '/admin/approve',
    },
    {
      href: '/admin/properties',
      label: 'Toàn bộ tin đăng',
      icon: ListFilter,
      match: (p: string) => p === '/admin/properties',
    },
  ]

  return (
    <div className="min-h-screen bg-[var(--background)]">
      {/* Admin Sidebar */}
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-64 border-r border-border bg-card p-6 lg:flex lg:flex-col lg:justify-between">
        <div>
          <div className="flex items-center justify-between">
            <Logo href="/admin" />
            <span className="rounded-full bg-destructive/15 text-destructive border border-destructive/30 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider">
              Admin
            </span>
          </div>

          <div className="mt-8">
            <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground px-3 mb-2">
              Quản trị hệ thống
            </p>
            <nav className="flex flex-col gap-1 text-sm font-medium" aria-label="Admin Navigation">
              {navLinks.map((l) => {
                const active = l.match(pathname)
                const Icon = l.icon

                return (
                  <Link
                    key={l.href}
                    href={l.href}
                    className={cn(
                      'flex items-center justify-between rounded-xl px-3.5 py-2.5 transition-colors',
                      active
                        ? 'bg-primary text-primary-foreground font-semibold shadow-xs'
                        : 'text-muted-foreground hover:bg-surface hover:text-foreground',
                    )}
                  >
                    <div className="flex items-center gap-3">
                      <Icon className="size-4 shrink-0" />
                      <span>{l.label}</span>
                    </div>
                    {typeof l.badge === 'number' && l.badge > 0 && (
                      <span
                        className={cn(
                          'rounded-full px-2 py-0.5 text-xs font-bold',
                          active ? 'bg-primary-foreground text-primary' : 'bg-destructive text-white',
                        )}
                      >
                        {l.badge}
                      </span>
                    )}
                  </Link>
                )
              })}
            </nav>
          </div>
        </div>

        <div className="space-y-4 border-t border-border pt-4">
          <Link
            href="/"
            className="flex items-center gap-2 rounded-xl border border-border px-3.5 py-2 text-xs text-muted-foreground hover:bg-surface hover:text-foreground transition-colors"
          >
            <ArrowLeft className="size-3.5" /> Xem trang khách hàng
          </Link>

          <div className="flex items-center justify-between p-2 rounded-xl bg-surface">
            <div className="flex items-center gap-2 overflow-hidden">
              <div className="grid size-8 place-items-center rounded-full bg-destructive/20 text-destructive text-xs font-bold shrink-0">
                AD
              </div>
              <div className="truncate">
                <p className="text-xs font-semibold truncate">Quản trị viên</p>
                <p className="text-[10px] text-muted-foreground">admin@propnest.vn</p>
              </div>
            </div>
            <button
              onClick={() => {
                signOut()
                router.push('/login?role=admin')
              }}
              className="text-muted-foreground hover:text-destructive p-1"
              title="Đăng xuất"
              aria-label="Đăng xuất"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Top Header */}
      <header className="sticky top-0 z-30 h-16 border-b border-border bg-card/80 backdrop-blur-md px-6 flex items-center justify-between lg:ml-64">
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-5 text-primary" />
          <h2 className="text-sm font-semibold text-foreground">Cổng Quản trị & Kiểm duyệt Bất động sản PropNest</h2>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-xs text-muted-foreground">
            {pendingCount} tin đang đợi phê duyệt
          </span>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="p-6 lg:ml-64 lg:p-8">{children}</main>
    </div>
  )
}
