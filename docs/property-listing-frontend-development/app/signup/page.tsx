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

function SignupForm() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const defaultRole = (searchParams.get('role') as Role) || 'customer'

  const [role, setRole] = useState<Role>(defaultRole)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const { signIn } = useStore()

  useEffect(() => {
    const r = searchParams.get('role') as Role
    if (r && ['customer', 'seller'].includes(r)) {
      setRole(r)
    }
  }, [searchParams])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()

    signIn({
      name: name || (role === 'seller' ? 'Người bán Mới' : 'Khách hàng Mới'),
      email: email || 'user@propnest.vn',
      role,
      id: role === 'seller' ? 'seller-1' : 'user-new',
    })

    if (role === 'seller') {
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
      title: 'Người bán / Môi giới',
      desc: 'Đăng tin BĐS, mua gói VIP & ví',
      icon: Store,
    },
  ]

  return (
    <main className="grid min-h-screen lg:grid-cols-2">
      {/* Left visual panel */}
      <div className="hidden bg-[#1f1a14] p-12 text-[#fffaf0] lg:flex lg:flex-col lg:justify-between border-r border-border/20">
        <div>
          <Logo href="/" />
          <span className="ml-2 inline-block rounded-full bg-primary/20 text-primary border border-primary/40 px-2.5 py-0.5 text-xs font-semibold">
            Tạo tài khoản PropNest
          </span>
        </div>

        <div className="space-y-6">
          <p className="font-display text-5xl font-medium leading-tight">
            Khởi đầu hành trình <br />
            <em className="text-primary italic">cùng PropNest hôm nay.</em>
          </p>
          <p className="max-w-md text-sm text-[#fffaf0]/70 leading-relaxed">
            Cho dù bạn đang tìm kiếm căn nhà mơ ước hay là nhà môi giới muốn tiếp cận hàng triệu khách hàng, PropNest mang đến giải pháp công nghệ hàng đầu.
          </p>
        </div>

        <p className="text-xs text-[#fffaf0]/40">
          © 2026 PropNest Real Estate Platform.
        </p>
      </div>

      {/* Right form panel */}
      <div className="flex items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-md space-y-6">
          <div className="lg:hidden mb-6">
            <Logo href="/" />
          </div>

          <div>
            <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Đăng ký tài khoản</p>
            <h1 className="font-display text-3xl font-bold mt-1 text-foreground">Tạo tài khoản PropNest</h1>
            <p className="text-xs text-muted-foreground mt-1">
              Điền thông tin bên dưới để bắt đầu sử dụng hệ thống.
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label className="text-xs font-semibold text-foreground mb-2 block">
                Loại tài khoản đăng ký:
              </label>
              <div className="grid grid-cols-2 gap-3">
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
                      <Icon className="size-5 mb-1.5 shrink-0" />
                      <strong className="block">{rc.title}</strong>
                      <span className="text-[10px] text-muted-foreground mt-0.5">{rc.desc}</span>
                    </button>
                  )
                })}
              </div>
            </div>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">Họ và tên *</label>
                <Input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Ví dụ: Nguyễn Văn A"
                  required
                />
              </div>

              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">Địa chỉ Email *</label>
                <Input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  required
                />
              </div>

              <div>
                <label className="text-xs font-medium text-muted-foreground mb-1 block">Mật khẩu *</label>
                <div className="relative">
                  <Input
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Tối thiểu 6 ký tự"
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
              Đăng ký ngay <ArrowRight className="size-4 ml-1" />
            </Button>
          </form>

          <div className="border-t border-border pt-4 text-center text-xs text-muted-foreground">
            Đã có tài khoản?{' '}
            <Link href={`/login?role=${role}`} className="font-semibold text-primary hover:underline">
              Đăng nhập tại đây
            </Link>
          </div>
        </div>
      </div>
    </main>
  )
}

export default function SignupPage() {
  return (
    <Suspense fallback={<div className="flex h-screen items-center justify-center">Đang tải...</div>}>
      <SignupForm />
    </Suspense>
  )
}
