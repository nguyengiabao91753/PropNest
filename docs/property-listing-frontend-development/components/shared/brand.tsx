import Link from 'next/link'
import { cn } from '@/lib/utils'

export function Brand({ dark = false }: { dark?: boolean }) {
  return <Logo tone={dark ? 'dark' : 'light'} />
}

export function Logo({ href = '/', className, tone = 'dark' }: { href?: string; className?: string; tone?: 'dark' | 'light' }) {
  return (
    <Link
      href={href}
      className={cn(
        'inline-flex items-center gap-2 font-display text-2xl italic font-semibold tracking-tight',
        tone === 'light' ? 'text-background' : 'text-foreground',
        className,
      )}
      aria-label="PropNest home"
    >
      <svg viewBox="0 0 32 32" className="size-7" aria-hidden="true">
        <circle cx="16" cy="16" r="10" fill="currentColor" />
        <ellipse cx="16" cy="16" rx="15" ry="5" fill="none" stroke="var(--primary)" strokeWidth="2.4" transform="rotate(-20 16 16)" />
      </svg>
      PropNest
    </Link>
  )
}

export function Sparkle({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-6 text-primary', className)} aria-hidden="true">
      <path
        d="M12 1v22M1 12h22M4.2 4.2l15.6 15.6M19.8 4.2 4.2 19.8"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
      />
    </svg>
  )
}

export function SectionTitle({
  lead,
  accent,
  className,
  sparkles = true,
}: {
  lead: string
  accent: string
  className?: string
  sparkles?: boolean
}) {
  return (
    <div className={cn('flex items-center justify-center gap-4 md:gap-6', className)}>
      {sparkles && <Sparkle className="size-5 md:size-7" />}
      <h2 className="font-display text-3xl text-balance md:text-5xl">
        {lead} <em className="font-medium">{accent}</em>
      </h2>
      {sparkles && <Sparkle className="size-5 md:size-7" />}
    </div>
  )
}

export function XIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-4', className)} fill="currentColor" aria-hidden="true">
      <path d="M18.9 2H22l-6.8 7.8L23 22h-6.3l-4.9-6.4L6.2 22H3.1l7.3-8.3L1 2h6.4l4.4 5.9L18.9 2Zm-1.1 18h1.7L6.3 3.9H4.5L17.8 20Z" />
    </svg>
  )
}

export function LinkedInIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-4', className)} fill="currentColor" aria-hidden="true">
      <path d="M4.98 3.5a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5ZM3 9.75h4v11H3v-11Zm7 0h3.8v1.6h.06c.53-1 1.83-2.05 3.77-2.05 4.03 0 4.77 2.6 4.77 6v5.45h-4v-4.83c0-1.15-.02-2.63-1.6-2.63-1.6 0-1.85 1.25-1.85 2.55v4.91h-4v-11Z" />
    </svg>
  )
}

export function InstagramIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-4', className)} fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <rect x="3" y="3" width="18" height="18" rx="5" />
      <circle cx="12" cy="12" r="4" />
      <circle cx="17.5" cy="6.5" r="1" fill="currentColor" />
    </svg>
  )
}

export function FacebookIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-4', className)} fill="currentColor" aria-hidden="true">
      <path d="M13.5 22v-8h2.7l.4-3.2h-3.1V8.8c0-.9.3-1.6 1.6-1.6h1.7V4.4c-.3 0-1.3-.1-2.5-.1-2.5 0-4.2 1.5-4.2 4.3v2.3H7.4V14h2.7v8h3.4Z" />
    </svg>
  )
}
