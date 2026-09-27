import { Analytics } from '@vercel/analytics/next'
import type { Metadata, Viewport } from 'next'
import { Roboto_Slab, Zilla_Slab } from 'next/font/google'
import { StoreProvider } from '@/lib/store'
import { Toaster } from '@/components/ui/sonner'
import './globals.css'

const robotoSlab = Roboto_Slab({ subsets: ['latin'], variable: '--font-roboto-slab', display: 'swap' })
const zillaSlab = Zilla_Slab({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  style: ['normal', 'italic'],
  variable: '--font-zilla-slab',
  display: 'swap',
})

export const metadata: Metadata = {
  title: {
    default: 'PropNest — Nền tảng Đăng tin Bất động sản',
    template: '%s | PropNest',
  },
  description:
    'PropNest kết nối người mua, thuê và người bán bất động sản với danh sách tin đăng đã qua kiểm duyệt tin cậy. Quản lý tin, mua gói hiển thị và quy trình phê duyệt minh bạch.',
  generator: 'v0.app',
  icons: {
    icon: [
      { url: '/icon-light-32x32.png', media: '(prefers-color-scheme: light)' },
      { url: '/icon-dark-32x32.png', media: '(prefers-color-scheme: dark)' },
      { url: '/icon.svg', type: 'image/svg+xml' },
    ],
    apple: '/apple-icon.png',
  },
}

export const viewport: Viewport = {
  colorScheme: 'light',
  themeColor: '#fff8e3',
}

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className={`${robotoSlab.variable} ${zillaSlab.variable}`}>
      <body className="antialiased">
        <StoreProvider>
          {children}
          <Toaster position="top-center" richColors />
        </StoreProvider>
        {process.env.NODE_ENV === 'production' && <Analytics />}
      </body>
    </html>
  )
}
