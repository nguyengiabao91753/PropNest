import { SellerHeader } from '@/components/seller/seller-header'

export default function SellerLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-dvh bg-surface">
      <SellerHeader />
      <main className="min-h-dvh lg:ml-64">{children}</main>
    </div>
  )
}
