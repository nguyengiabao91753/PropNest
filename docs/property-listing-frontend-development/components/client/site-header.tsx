'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { ChevronDown, Heart, LayoutDashboard, LogOut, Menu, Store, UserRound } from 'lucide-react'
import { Logo } from '@/components/shared/brand'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { useStore } from '@/lib/store'
import { cn } from '@/lib/utils'

const listingLinks = [
  { href: '/listings?type=sale', label: 'BĐS Cần bán' },
  { href: '/listings?type=rent', label: 'BĐS Cho thuê' },
  { href: '/listings?home=Villa', label: 'Biệt thự (Villa)' },
  { href: '/listings?home=Apartment', label: 'Căn hộ chung cư' },
  { href: '/listings?home=House', label: 'Nhà phố' },
]

function NavDropdown({ label, items, active }: { label: string; items: { href: string; label: string }[]; active?: boolean }) {
  const router = useRouter()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(
          'flex items-center gap-1.5 py-1 text-sm outline-none transition-colors hover:text-primary focus-visible:text-primary',
          active ? 'text-primary font-semibold' : 'text-muted-foreground',
        )}
      >
        {label}
        <ChevronDown className="size-4" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-48">
        {items.map((i) => (
          <DropdownMenuItem key={i.href} onClick={() => router.push(i.href)}>
            {i.label}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function NavLinks({ pathname }: { pathname: string }) {
  const linkCls = (active: boolean) =>
    cn(
      'relative py-1 text-sm font-medium transition-colors hover:text-primary',
      active ? 'text-primary font-semibold after:absolute after:inset-x-0 after:-bottom-1.5 after:h-0.5 after:bg-primary' : 'text-muted-foreground',
    )
  return (
    <>
      <Link href="/" className={linkCls(pathname === '/')}>
        Trang chủ
      </Link>
      <Link href="/listings" className={linkCls(pathname === '/listings')}>
        Khám phá BĐS
      </Link>
      <NavDropdown label="Danh mục" items={listingLinks} active={pathname.startsWith('/listings')} />
      <Link href="/#about" className={linkCls(false)}>
        Về PropNest
      </Link>
    </>
  )
}

function AccountArea() {
  const { session, signOut, favorites } = useStore()
  const router = useRouter()

  if (!session) {
    return (
      <div className="flex items-center gap-2">
        <Link
          href="/seller/properties"
          className="hidden md:inline-flex items-center gap-1.5 text-xs font-semibold text-muted-foreground hover:text-foreground border border-border rounded-full px-3 py-1.5 hover:bg-surface transition-colors"
        >
          <Store className="size-3.5 text-primary" /> Kênh Người bán
        </Link>
        <Link
          href="/admin"
          className="hidden md:inline-flex items-center gap-1.5 text-xs font-semibold text-muted-foreground hover:text-foreground border border-border rounded-full px-3 py-1.5 hover:bg-surface transition-colors"
        >
          <LayoutDashboard className="size-3.5 text-destructive" /> Kênh Quản trị
        </Link>
        <Link
          href="/login"
          className="text-xs font-medium text-muted-foreground hover:text-primary px-3 py-1.5"
        >
          Đăng nhập
        </Link>
        <Button
          render={<Link href="/signup" />}
          nativeButton={false}
          className="h-9 rounded-full px-4 text-xs font-semibold"
        >
          Đăng ký
        </Button>
      </div>
    )
  }

  return (
    <div className="flex items-center gap-2">
      {/* Quick Portal Switchers */}
      <Link
        href="/seller/properties"
        className="hidden md:inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground border border-border rounded-full px-3 py-1.5 hover:bg-surface transition-colors"
      >
        <Store className="size-3.5 text-primary" /> Cổng Người bán
      </Link>
      <Link
        href="/admin"
        className="hidden md:inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground border border-border rounded-full px-3 py-1.5 hover:bg-surface transition-colors"
      >
        <LayoutDashboard className="size-3.5 text-destructive" /> Cổng Admin
      </Link>

      <DropdownMenu>
        <DropdownMenuTrigger className="flex items-center gap-2 rounded-full border border-primary/40 bg-brand-soft/40 px-3 py-1.5 text-xs font-medium text-foreground outline-none hover:bg-brand-soft">
          <UserRound className="size-3.5 text-primary" aria-hidden="true" />
          <span className="max-w-28 truncate">{session.name}</span>
          <span className="rounded bg-primary/20 text-primary text-[10px] font-bold px-1.5 py-0.2 uppercase">
            {session.role}
          </span>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-52">
          <DropdownMenuGroup>
            <DropdownMenuLabel>Tài khoản: {session.name}</DropdownMenuLabel>
            <DropdownMenuItem onClick={() => router.push('/listings?saved=1')}>
              <Heart className="size-4 mr-2" /> BĐS đã lưu ({favorites.size})
            </DropdownMenuItem>
          </DropdownMenuGroup>
          <DropdownMenuSeparator />
          <DropdownMenuGroup>
            <DropdownMenuItem onClick={() => router.push('/seller/properties')}>
              <Store className="size-4 mr-2 text-primary" /> Cổng Người bán (Seller)
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => router.push('/admin')}>
              <LayoutDashboard className="size-4 mr-2 text-destructive" /> Cổng Quản trị (Admin)
            </DropdownMenuItem>
          </DropdownMenuGroup>
          <DropdownMenuSeparator />
          <DropdownMenuGroup>
            <DropdownMenuItem
              variant="destructive"
              onClick={() => {
                signOut()
                router.push('/login')
              }}
            >
              <LogOut className="size-4 mr-2" /> Đăng xuất
            </DropdownMenuItem>
          </DropdownMenuGroup>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  )
}

export function SiteHeader({ variant = 'default' }: { variant?: 'default' | 'centered' }) {
  const pathname = usePathname()

  const mobileMenu = (
    <Sheet>
      <SheetTrigger render={<Button variant="ghost" size="icon" className="md:hidden" aria-label="Mở menu" />}>
        <Menu className="size-5" />
      </SheetTrigger>
      <SheetContent side="left" className="bg-background p-6">
        <SheetTitle className="sr-only">Menu Điều Hướng</SheetTitle>
        <Logo />
        <nav className="mt-8 flex flex-col gap-4 text-sm font-medium" aria-label="Mobile Navigation">
          <Link href="/">Trang chủ</Link>
          <Link href="/listings">Khám phá BĐS</Link>
          <Link href="/listings?type=sale">Cần bán</Link>
          <Link href="/listings?type=rent">Cho thuê</Link>
          <div className="border-t border-border pt-4 space-y-2">
            <Link href="/seller/properties" className="block text-primary font-semibold">
              Cổng Người bán (Seller Portal)
            </Link>
            <Link href="/admin" className="block text-destructive font-semibold">
              Cổng Quản trị (Admin Portal)
            </Link>
          </div>
        </nav>
      </SheetContent>
    </Sheet>
  )

  return (
    <header className="sticky top-0 z-40 w-full border-b border-border bg-card/85 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        <div className="flex items-center gap-8">
          <Logo href="/" />
          <nav className="hidden items-center gap-6 md:flex" aria-label="Customer Navigation">
            <NavLinks pathname={pathname} />
          </nav>
        </div>

        <div className="flex items-center gap-3">
          <AccountArea />
          {mobileMenu}
        </div>
      </div>
    </header>
  )
}
