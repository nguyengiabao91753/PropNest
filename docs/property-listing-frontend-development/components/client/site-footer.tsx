import Link from 'next/link'
import { FacebookIcon, InstagramIcon, LinkedInIcon, XIcon } from '@/components/shared/brand'

const links = [
  { href: '/', label: 'Trang chủ' },
  { href: '/listings', label: 'Bất động sản' },
  { href: '/#about', label: 'Về PropNest' },
  { href: '/seller/properties', label: 'Kênh Người bán' },
  { href: '/admin', label: 'Kênh Quản trị' },
]

export function SiteFooter() {
  return (
    <footer id="contact" className="bg-foreground text-background">
      <div className="mx-auto max-w-7xl px-4 pt-10 md:px-8">
        <div className="flex flex-col items-center justify-between gap-6 text-sm md:flex-row">
          <p className="text-background/70">&copy; 2026 PropNest Inc. Bảo lưu mọi quyền.</p>
          <nav className="flex flex-wrap justify-center gap-6 text-xs font-medium" aria-label="Footer">
            {links.map((l) => (
              <Link key={l.label} href={l.href} className="text-background/80 hover:text-primary transition-colors">
                {l.label}
              </Link>
            ))}
          </nav>
          <div className="flex items-center gap-4">
            {[
              { Icon: XIcon, label: 'X' },
              { Icon: FacebookIcon, label: 'Facebook' },
              { Icon: InstagramIcon, label: 'Instagram' },
              { Icon: LinkedInIcon, label: 'LinkedIn' },
            ].map(({ Icon, label }) => (
              <a key={label} href="#" aria-label={label} className="text-background/80 hover:text-primary transition-colors">
                <Icon />
              </a>
            ))}
          </div>
        </div>
        <p
          aria-hidden="true"
          className="mt-6 select-none bg-cover bg-center bg-clip-text text-center font-display text-[20vw] leading-[0.8] font-bold tracking-tight text-transparent md:text-[16rem]"
          style={{ backgroundImage: "url('/images/hero-home.png')" }}
        >
          PROPNEST
        </p>
      </div>
    </footer>
  )
}
