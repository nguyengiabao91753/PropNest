'use client'

import { Suspense, useEffect, useState } from 'react'
import Link from 'next/link'
import { useRouter, useSearchParams } from 'next/navigation'
import { ArrowRight, Eye, EyeOff, LayoutDashboard, ShieldCheck, Store, User } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Logo } from '@/components/shared/brand'
import { useStore, type Role } from '@/lib/store'
import { cn } from '@/lib/utils'

function LoginForm() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const defaultRole = (searchParams.get('role') as Role) || 'customer'

  const [role, setRole] = useState<Role>(defaultRole)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const { signIn } = useStore()

  useEffect(() => {
    const r = searchParams.get('role') as Role
    if (r && ['customer', 'seller', 'admin'].includes(r)) {
      setRole(r)
    }
  }, [searchParams])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()

    const roleNames: Record<Role, string> = {
      customer: 'Khách hàng',
      seller: 'Nguyễn Văn Minh (Người bán)',
      admin: 'Quản trị viên Hệ thống',
      moderator: 'Kiểm duyệt viên',
    }

    const roleEmails: Record<Role, string> = {
      customer: email || 'khachhang@example.com',
      seller: email || 'minh.nguyen@propnest.vn',
      admin: email || 'admin@propnest.vn',
      moderator: email || 'moderator@propnest.vn',
    }

    signIn({
      name: roleNames[role],
      email: roleEmails[role],
      role,
      id: role === 'seller' ? 'seller-1' : role === 'admin' ? 'admin-1' : 'user-1',
    })

    if (role === 'admin') {
      router.push('/admin')
    } else if (role === 'seller') {
      router.push('/seller/properties')
    } else {
      router.push('/')
    }
  }

  const roleCards = [
    {
      id: 'customer' as Role,
      title: 'Khách hàng',
      desc: 'Tìm kiếm BĐS, lưu tin & liên hệ',
      icon: User,
    },
    {
      id: 'seller' as Role,
      title: 'Người bán (Seller)',
      desc: 'Đăng tin BĐS, mua gói VIP & ví',
      icon: Store,
    },
    {
      id: 'admin' as Role,
      title: 'Quản trị (Admin)',
      desc: 'Duyệt tin đăng & quản trị hệ thống',
      icon: LayoutDashboard,
    },
  ]

  return (
    <main className="grid min-h-screen lg:grid-cols-2">
      {/* Left visual panel */}
      <div className="hidden bg-[#1f1a14] p-12 text-[#fffaf0] lg:flex lg:flex-col lg:justify-between border-r border-border/20">
        <div>
          <Logo href="/" />
          <span className="ml-2 inline-block rounded-full bg-primary/20 text-primary border border-primary/40 px-2.5 py-0.5 text-xs font-semibold">
            Nền tảng BĐS Toàn diện
          </span>
        </div>

        <div className="space-y-6">
          <p className="font-display text-5xl font-medium leading-tight">
            Nâng tầm trải nghiệm <br />
            <em className="text-primary italic">giao dịch bất động sản.</em>
          </p>
          <p className="max-w-md text-sm text-[#fffaf0]/70 leading-relaxed">
            Hệ thống phân quyền chuẩn mực cho Khách hàng, Người bán và Ban Quản trị. Minh bạch trong kiểm duyệt, tức thì trong điều phối Saga.
          </p>

          <div className="grid grid-cols-3 gap-4 pt-4 border-t border-white/10 text-xs">
            <div>
              <p className="text-2xl font-bold font-display text-primary">8 States</p>
              <p className="text-white/60 mt-1">Vòng đời tin đăng</p>
            </div>
            <div>
              <p className="text-2xl font-bold font-display text-primary">100%</p>
              <p className="text-white/60 mt-1">Audit Delta History</p>
            </div>
            <div>
              <p className="text-2xl font-bold font-display text-primary">Saga</p>
              <p className="text-white/60 mt-1">Orchestration</p>
            </div>
          </div>
        </div>

        <p className="text-xs text-[#fffaf0]/40">
          © 2026 PropNest Real Estate Platform. Bản quyền được bảo lưu.
        </p>
      </div>

      {/* Right form panel */}
      <div className="flex items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-md space-y-6">
          <div className="lg:hidden mb-6">
            <Logo href="/" />
          </div>

          <div>
            <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Đăng nhập tài khoản</p>
            <h1 className="font-display text-3xl font-bold mt-1 text-foreground">Chào mừng trở lại PropNest</h1>
            <p className="text-xs text-muted-foreground mt-1">
              Chọn vai trò truy cập để đăng nhập vào phân hệ tương ứng.
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Role selection tabs */}
            <div>
              <label className="text-xs font-semibold text-foreground mb-2 block">
                Đăng nhập với vai trò:
              </label>
              <div className="grid grid-cols-3 gap-2">
                {roleCards.map((rc) => {
                  const Icon = rc.icon
                  const active = role === rc.id
                  return (
                    <button
                      type="button"
                      key={rc.id}
                      onClick={() => setRole(rc.id)}
                      className={cn(
                        'flex flex-col items-center text-center p-3 rounded-xl border transition-all text-xs',
                        active
                          ? 'border-primary bg-brand-soft text-primary font-bold shadow-xs'
                          : 'border-border bg-card text-muted-foreground hover:border-primary/50 hover:text-foreground',
                      )}
                    >
                      <Icon className="size-4 mb-1.5 shrink-0" />
                      <span className="truncate w-full">{rc.title}</span>
                    </button>
                  )
                })}
              </div>
            </div>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">Email</label>
                <Input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={
                    role === 'admin'
                      ? 'admin@propnest.vn'
                      : role === 'seller'
                        ? 'minh.nguyen@propnest.vn'
                        : 'khachhang@example.com'
                  }
                  required
                />
              </div>

              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">Mật khẩu</label>
                <div className="relative">
                  <Input
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••"
                    required
                    minLength={6}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-2.5 text-muted-foreground hover:text-foreground"
                    aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  >
                    {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                  </button>
                </div>
              </div>
            </div>

            <Button className="w-full text-xs font-semibold py-3" size="lg" type="submit">
              Đăng nhập vào {roleCards.find((r) => r.id === role)?.title} <ArrowRight className="size-4 ml-1" />
            </Button>
          </form>

          <div className="border-t border-border pt-4 text-center text-xs text-muted-foreground">
            Chưa có tài khoản?{' '}
            <Link href={`/signup?role=${role}`} className="font-semibold text-primary hover:underline">
              Đăng ký tài khoản mới
            </Link>
          </div>
        </div>
      </div>
    </main>
  )
}

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="flex h-screen items-center justify-center">Đang tải...</div>}>
      <LoginForm />
    </Suspense>
  )
}
