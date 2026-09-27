export type ListingType = 'sale' | 'rent'

// 8 Finite States from PropNest Blueprint
export type ListingStatus =
  | 'Draft'
  | 'PendingPayment'
  | 'PendingModeration'
  | 'Published'
  | 'Rejected'
  | 'Hidden'
  | 'Expired'
  | 'Deleted'

// Backwards compatibility alias
export type PropertyStatus = ListingStatus

export type PropertyType = 'House' | 'Apartment' | 'Villa' | 'Townhouse' | 'Commercial'
export type Category = 'Residential' | 'Commercial' | 'Apartments'

// Monetization & Packages (F05)
export type PackageCode = 'Standard' | 'VIP' | 'Boost'

export type PackageDefinition = {
  code: PackageCode
  name: string
  price: number // in VND
  durationDays: number
  description: string
  badge: string
  highlight: boolean
  features: string[]
}

export const PACKAGES: PackageDefinition[] = [
  {
    code: 'Standard',
    name: 'Gói Chuẩn (Standard)',
    price: 0,
    durationDays: 30,
    description: 'Đăng tin thông thường, hiển thị theo thứ tự thời gian.',
    badge: 'Miễn phí',
    highlight: false,
    features: ['Thời hạn 30 ngày', 'Hiển thị tìm kiếm cơ bản', 'Hỗ trợ duyệt tin tiêu chuẩn'],
  },
  {
    code: 'VIP',
    name: 'Gói VIP Nổi Bật',
    price: 500_000,
    durationDays: 30,
    description: 'Ghim đầu danh mục tìm kiếm, viền vàng kim, tăng gấp 5 lần lượt xem.',
    badge: 'Phổ biến nhất',
    highlight: true,
    features: ['Ghim TOP 1 trang tìm kiếm', 'Huy hiệu VIP viền vàng kim', 'Ưu tiên duyệt tin trong 15 phút', 'Thời hạn 30 ngày'],
  },
  {
    code: 'Boost',
    name: 'Gói Đẩy Tin (Boost)',
    price: 200_000,
    durationDays: 14,
    description: 'Tự động làm mới ngày đăng và đẩy lên đầu trang tìm kiếm mỗi ngày.',
    badge: 'Đẩy top',
    highlight: false,
    features: ['Tự động đẩy top mỗi ngày lúc 10h', 'Huy hiệu Hot Listing', 'Thời hạn 14 ngày'],
  },
]

// Wallet & Ledger (F05)
export type Wallet = {
  id: string
  userId: string
  mainBalance: number // Số dư chính (VND)
  promoBalance: number // Số dư khuyến mãi (VND)
  rowVersion: string
}

export type WalletTransaction = {
  id: string
  correlationId: string
  type: 'Deposit' | 'Charge' | 'Refund'
  amount: number
  balanceBefore: number
  balanceAfter: number
  description: string
  createdAt: string
}

// Saga Orchestrator & Workflows (F06, F08)
export type WorkflowStatus =
  | 'Started'
  | 'DeductingWallet'
  | 'WalletDeducted'
  | 'UpgradingListing'
  | 'RecordingHistory'
  | 'Completed'
  | 'Compensating'
  | 'Compensated'
  | 'Failed'

export type WorkflowStep = {
  name: string
  status: 'pending' | 'in_progress' | 'completed' | 'failed'
  timestamp: string
  error?: string
}

export type WorkflowInstance = {
  correlationId: string
  listingId: string
  currentState: WorkflowStatus
  packageCode: PackageCode
  chargeAmount: number
  createdAt: string
  steps: WorkflowStep[]
  errorMessage?: string
}

// History Delta Engine (F04)
export type ListingHistory = {
  historyId: string
  listingId: string
  actor: string
  actionType: 'Create' | 'Update' | 'Submit' | 'Approve' | 'Reject' | 'UpgradePackage' | 'Expire' | 'Hide'
  actionDate: string
  note?: string
  deltaChanges: {
    field: string
    oldValue: any
    newValue: any
  }[]
}

export type Property = {
  id: string
  title: string
  address: string
  streetNumber: string
  ward: string
  district: string
  city: string
  suburb: string // compatibility (equals district)
  council: string // compatibility (equals city)
  lat: number
  lng: number
  price: number // in VND
  listingType: ListingType
  propertyType: PropertyType
  category: Category
  beds: number
  baths: number
  carports: number
  area: number // m²
  sqft: number // kept for layout backward compatibility (m²)
  landSize: number // m²
  condition: string
  facilities: string[]
  notes: string
  displayNotes: boolean
  description: string
  images: string[]
  rating: number
  reviews: number
  status: ListingStatus
  packageCode?: PackageCode
  rejectReason?: string
  sellerId: string
  sellerName: string
  sellerAvatar: string
  createdAt: string
  rowVersion: string
  featured: boolean
  banner?: string
  showAddress: boolean
  views: number
  inquiries: number
}

// Vietnam Administrative Location Model
export const VIETNAM_CITIES = ['TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng', 'Bình Dương'] as const

export const VIETNAM_LOCATIONS: Record<
  string,
  {
    coords: [number, number]
    districts: Record<string, string[]>
  }
> = {
  'TP. Hồ Chí Minh': {
    coords: [10.7769, 106.7009],
    districts: {
      'Quận 1': ['Bến Nghé', 'Bến Thành', 'Đa Kao', 'Tân Định', 'Cầu Kho'],
      'Quận 2 (Thủ Đức)': ['Thảo Điền', 'An Phú', 'Bình An', 'Thạnh Mỹ Lợi', 'Thủ Thiêm'],
      'Quận 3': ['Phường 1', 'Phường 2', 'Phường 3', 'Võ Thị Sáu'],
      'Quận 7': ['Tân Phong (Phú Mỹ Hưng)', 'Tân Phú', 'Tân Quy', 'Phú Mỹ'],
      'Bình Thạnh': ['Phường 19', 'Phường 22 (Vinhomes)', 'Phường 25', 'Phường 26'],
      'Phú Nhuận': ['Phường 1', 'Phường 2', 'Phường 8', 'Phường 15'],
    },
  },
  'Hà Nội': {
    coords: [21.0285, 105.8542],
    districts: {
      'Hoàn Kiếm': ['Hàng Bạc', 'Hàng Trống', 'Tràng Tiền', 'Cửa Nam'],
      'Cầu Giấy': ['Dịch Vọng', 'Dịch Vọng Hậu', 'Yên Hòa', 'Trung Hòa'],
      'Tây Hồ': ['Quảng An', 'Xuân La', 'Yên Phụ', 'Tứ Liên'],
      'Ba Đình': ['Kim Mã', 'Liễu Giai', 'Điện Biên', 'Cống Vị'],
      'Nam Từ Liêm': ['Mỹ Đình 1', 'Mỹ Đình 2', 'Mễ Trì', 'Trung Văn'],
    },
  },
  'Đà Nẵng': {
    coords: [16.0544, 108.2022],
    districts: {
      'Hải Châu': ['Hải Châu 1', 'Hải Châu 2', 'Thạch Thang', 'Hòa Thuận Đông'],
      'Sơn Trà': ['An Hải Bắc', 'Phước Mỹ', 'Thọ Quang', 'Mân Thái'],
      'Ngũ Hành Sơn': ['Khuê Mỹ', 'Mỹ An', 'Hòa Hải'],
    },
  },
  'Bình Dương': {
    coords: [10.9804, 106.6519],
    districts: {
      'Thủ Dầu Một': ['Phú Cường', 'Phú Hòa', 'Hiệp Thành', 'Định Hòa'],
      'Thuận An': ['Lái Thiêu', 'An Phú', 'Bình Hòa'],
    },
  },
}

export const FACILITIES = [
  'None',
  'Hồ bơi vô cực',
  'Phòng Gym hiện đại',
  'Sân tennis / Thể thao',
  'Khu vui chơi trẻ em',
  'Thang máy tốc độ cao',
  'Bảo vệ 24/7',
  'Chỗ đỗ ô tô',
] as const

export const PROPERTY_TYPES: PropertyType[] = ['House', 'Apartment', 'Villa', 'Townhouse', 'Commercial']
export const CONDITIONS = ['Xây mới hoàn toàn', 'Nhà mới đẹp', 'Nội thất cao cấp', 'Cần sửa chữa nhẹ'] as const

// Mock Suburbs for compatibility
export const SUBURBS = [
  'Quận 1',
  'Quận 2 (Thủ Đức)',
  'Quận 3',
  'Quận 7',
  'Bình Thạnh',
  'Hoàn Kiếm',
  'Cầu Giấy',
  'Tây Hồ',
  'Hải Châu',
  'Sơn Trà',
] as const

export const SUBURB_COORDS: Record<string, [number, number]> = {
  'Quận 1': [10.7769, 106.7009],
  'Quận 2 (Thủ Đức)': [10.8016, 106.7388],
  'Quận 3': [10.7844, 106.6843],
  'Quận 7': [10.7324, 106.7157],
  'Bình Thạnh': [10.8038, 106.7088],
  'Hoàn Kiếm': [21.0313, 105.8524],
  'Cầu Giấy': [21.0333, 105.7939],
  'Tây Hồ': [21.0664, 105.8236],
  'Hải Châu': [16.0617, 108.2208],
  'Sơn Trà': [16.0821, 108.2435],
}

const exteriors = ['/images/p1.png', '/images/p2.png', '/images/p3.png', '/images/p4.png', '/images/p5.png', '/images/p6.png']
const interiors = ['/images/i1.png', '/images/i2.png', '/images/i3.png']

export const sellers = [
  { id: 'seller-1', name: 'Nguyễn Văn Minh', avatar: '/images/avatar-2.png', email: 'minh.nguyen@propnest.vn' },
  { id: 'seller-2', name: 'Trần Thị Thu Thảo', avatar: '/images/avatar-3.png', email: 'thao.tran@propnest.vn' },
  { id: 'seller-3', name: 'Lê Hoàng Nam', avatar: '/images/avatar-4.png', email: 'nam.le@propnest.vn' },
]

export const CURRENT_SELLER = sellers[0]

type Seed = [
  title: string,
  city: string,
  district: string,
  ward: string,
  address: string,
  price: number, // VND
  listingType: ListingType,
  propertyType: PropertyType,
  beds: number,
  baths: number,
  area: number, // m²
  status: ListingStatus,
  packageCode: PackageCode,
  sellerIdx: number,
]

const seeds: Seed[] = [
  ['Biệt Thự Đơn Lập Thảo Điền Ven Sông', 'TP. Hồ Chí Minh', 'Quận 2 (Thủ Đức)', 'Thảo Điền', 'Số 42 Đường Nguyễn Văn Hưởng', 35_000_000_000, 'sale', 'Villa', 5, 5, 450, 'Published', 'VIP', 0],
  ['Căn Hộ Landmark 81 View Trực Diện Sông', 'TP. Hồ Chí Minh', 'Bình Thạnh', 'Phường 22 (Vinhomes)', '208 Nguyễn Hữu Cảnh', 11_500_000_000, 'sale', 'Apartment', 3, 2, 110, 'Published', 'VIP', 1],
  ['Nhà Phố Kinh Doanh Phố Cổ Hoàn Kiếm', 'Hà Nội', 'Hoàn Kiếm', 'Hàng Trống', 'Số 15 Phố Nhà Thờ', 28_000_000_000, 'sale', 'Townhouse', 4, 4, 120, 'Published', 'Boost', 0],
  ['Biệt Thự Vườn Tây Hồ Ban Công Hồ Tây', 'Hà Nội', 'Tây Hồ', 'Quảng An', 'Ngõ 28 Đặng Thai Mai', 42_000_000_000, 'sale', 'Villa', 4, 4, 380, 'Published', 'VIP', 2],
  ['Penthouse Duplex Phú Mỹ Hưng View Sân Golf', 'TP. Hồ Chí Minh', 'Quận 7', 'Tân Phong (Phú Mỹ Hưng)', 'Đường Nguyễn Lương Bằng', 45_000_000, 'rent', 'Apartment', 3, 3, 220, 'Published', 'Standard', 0],
  ['Shophouse Thương Mại Cầu Giấy Mặt Tiền 8m', 'Hà Nội', 'Cầu Giấy', 'Dịch Vọng Hậu', 'Duy Tân, Cầu Giấy', 18_500_000_000, 'sale', 'Commercial', 0, 4, 180, 'Published', 'Standard', 1],
  ['Biệt Thự Biển Sơn Trà Ngay Bãi Tắm Mỹ Khê', 'Đà Nẵng', 'Sơn Trà', 'Phước Mỹ', 'Đường Võ Nguyên Giáp', 24_000_000_000, 'sale', 'Villa', 4, 4, 320, 'Published', 'VIP', 2],
  ['Căn Hộ Cao Cấp Quận 1 Sài Gòn Pearl', 'TP. Hồ Chí Minh', 'Quận 1', 'Bến Nghé', 'Tôn Đức Thắng', 32_000_000, 'rent', 'Apartment', 2, 2, 85, 'Published', 'Boost', 0],
  ['Nhà Riêng Dịch Vọng Yên Tĩnh Ngõ Ô Tô', 'Hà Nội', 'Cầu Giấy', 'Dịch Vọng', 'Trần Thái Tông', 7_200_000_000, 'sale', 'House', 4, 3, 65, 'Draft', 'Standard', 0],
  ['Căn Hộ Studio Trung Tâm Quận 3 Đầy Đủ Tiện Nghi', 'TP. Hồ Chí Minh', 'Quận 3', 'Võ Thị Sáu', 'Nam Kỳ Khởi Nghĩa', 14_000_000, 'rent', 'Apartment', 1, 1, 45, 'PendingModeration', 'VIP', 0],
  ['Nhà Phố Liền Kề Khuê Mỹ Tiện Kinh Doanh', 'Đà Nẵng', 'Ngũ Hành Sơn', 'Khuê Mỹ', 'Lê Văn Hiến', 6_800_000_000, 'sale', 'Townhouse', 3, 3, 100, 'PendingModeration', 'Standard', 0],
  ['Biệt Thự Nghỉ Dưỡng Thạch Thang Kiến Trúc Pháp', 'Đà Nẵng', 'Hải Châu', 'Thạch Thang', 'Bạch Đằng', 19_500_000_000, 'sale', 'Villa', 4, 3, 260, 'PendingPayment', 'VIP', 0],
  ['Mặt Bằng Văn Phòng Quận 1 Hạng A Cho Thuê', 'TP. Hồ Chí Minh', 'Quận 1', 'Bến Thành', 'Lê Duẩn', 120_000_000, 'rent', 'Commercial', 0, 4, 300, 'Rejected', 'Standard', 0],
  ['Căn Hộ D’Edge Thảo Điền Hồ Bơi Đáy Kính', 'TP. Hồ Chí Minh', 'Quận 2 (Thủ Đức)', 'Thảo Điền', 'Nguyễn Văn Hưởng', 55_000_000, 'rent', 'Apartment', 3, 2, 140, 'Hidden', 'Standard', 0],
  ['Nhà Riêng Phú Nhuận Hẻm Xe Hơi 6m', 'TP. Hồ Chí Minh', 'Phú Nhuận', 'Phường 2', 'Phan Xích Long', 9_800_000_000, 'sale', 'House', 4, 3, 80, 'Expired', 'Standard', 0],
]

const descriptions = [
  'Bất động sản vị trí đắc địa với thiết kế hiện đại, ngập tràn ánh sáng tự nhiên. Pháp lý chuẩn chỉnh, sổ hồng trao tay, công chứng trong ngày. Gần các tiện ích đẳng cấp, bệnh viện quốc tế và trường học danh tiếng.',
  'Căn nhà được hoàn thiện tỉ mỉ bằng vật liệu cao cấp nhập khẩu. Không gian mở thoáng đãng, ban công rộng view panorama cực kỳ yên bình và thoáng mát. Thích hợp vừa ở vừa làm văn phòng công ty.',
  'Tọa lạc trên tuyến phố sầm uất, hạ tầng đồng bộ, an ninh 24/7. Nhà có gara ô tô riêng, hệ thống điện thông minh Smart Home, sân vườn tiểu cảnh xanh mát.',
]

function daysAgo(n: number) {
  const d = new Date('2026-09-26T09:00:00Z')
  d.setUTCDate(d.getUTCDate() - n)
  return d.toISOString()
}

export const initialProperties: Property[] = seeds.map((s, i) => {
  const [title, city, district, ward, address, price, listingType, propertyType, beds, baths, area, status, packageCode, sellerIdx] = s
  const coords = VIETNAM_LOCATIONS[city]?.coords ?? [10.7769, 106.7009]
  const seller = sellers[sellerIdx]
  const jitterLat = ((i * 37) % 11) / 1000 - 0.005
  const jitterLng = ((i * 29) % 11) / 1000 - 0.005
  const cover = exteriors[i % exteriors.length]
  return {
    id: `PN-${(1000 + i * 17).toString()}`,
    title,
    address,
    streetNumber: `${15 + i * 2}`,
    ward,
    district,
    city,
    suburb: district,
    council: city,
    lat: coords[0] + jitterLat,
    lng: coords[1] + jitterLng,
    price,
    listingType,
    propertyType,
    category: propertyType === 'Commercial' ? 'Commercial' : propertyType === 'Apartment' ? 'Apartments' : 'Residential',
    beds,
    baths,
    carports: (i % 3) + 1,
    area,
    sqft: area,
    landSize: Math.round(area * 1.2),
    condition: CONDITIONS[i % CONDITIONS.length],
    facilities: [['Hồ bơi vô cực', 'Phòng Gym hiện đại'], ['Khu vui chơi trẻ em'], ['Thang máy tốc độ cao', 'Bảo vệ 24/7']][i % 3],
    notes: '',
    displayNotes: false,
    description: descriptions[i % descriptions.length],
    images: [cover, ...interiors, exteriors[(i + 2) % exteriors.length]],
    rating: Number((4.6 + ((i * 7) % 5) / 10).toFixed(1)),
    reviews: 20 + ((i * 31) % 120),
    status,
    packageCode,
    rowVersion: `AAAAAA${i + 100}A=`,
    rejectReason: status === 'Rejected' ? 'Hình ảnh đăng tải mờ và sai lệch thông tin diện tích thực tế so với quy hoạch.' : undefined,
    sellerId: seller.id,
    sellerName: seller.name,
    sellerAvatar: seller.avatar,
    createdAt: daysAgo(i * 3 + 1),
    featured: packageCode === 'VIP',
    banner: packageCode === 'VIP' ? 'VIP Nổi Bật' : packageCode === 'Boost' ? 'Hot Listing' : undefined,
    showAddress: true,
    views: 240 + ((i * 123) % 1500),
    inquiries: (i * 3) % 17,
  }
})

export const initialWallet: Wallet = {
  id: 'wal-001',
  userId: 'seller-1',
  mainBalance: 2_500_000, // 2.5 triệu VND
  promoBalance: 500_000, // 500k VND
  rowVersion: 'AAAAAAWallet1=',
}

export const initialTransactions: WalletTransaction[] = [
  {
    id: 'tx-001',
    correlationId: 'b75f8241-1123-4567-890a-112233445566',
    type: 'Deposit',
    amount: 3_000_000,
    balanceBefore: 0,
    balanceAfter: 3_000_000,
    description: 'Nạp tiền tài khoản qua thẻ ngân hàng',
    createdAt: daysAgo(10),
  },
  {
    id: 'tx-002',
    correlationId: 'c1234567-2234-4567-890a-223344556677',
    type: 'Charge',
    amount: 500_000,
    balanceBefore: 3_000_000,
    balanceAfter: 2_500_000,
    description: 'Thanh toán Gói VIP cho tin đăng PN-1000',
    createdAt: daysAgo(4),
  },
]

export const initialHistories: ListingHistory[] = [
  {
    historyId: 'hist-001',
    listingId: 'PN-1000',
    actor: 'Nguyễn Văn Minh (Seller)',
    actionType: 'Create',
    actionDate: daysAgo(5),
    note: 'Tạo bản nháp tin đăng ban đầu',
    deltaChanges: [
      { field: 'Status', oldValue: null, newValue: 'Draft' },
      { field: 'Price', oldValue: null, newValue: '35,000,000,000 ₫' },
    ],
  },
  {
    historyId: 'hist-002',
    listingId: 'PN-1000',
    actor: 'Nguyễn Văn Minh (Seller)',
    actionType: 'Update',
    actionDate: daysAgo(4),
    note: 'Cập nhật giá bán và mô tả tiện ích',
    deltaChanges: [
      { field: 'Price', oldValue: '37,000,000,000 ₫', newValue: '35,000,000,000 ₫' },
      { field: 'Condition', oldValue: 'Xây mới', newValue: 'Nội thất cao cấp' },
    ],
  },
  {
    historyId: 'hist-003',
    listingId: 'PN-1000',
    actor: 'Saga Orchestrator',
    actionType: 'UpgradePackage',
    actionDate: daysAgo(4),
    note: 'Nâng cấp Gói VIP thành công (Correlation: c1234567-...)',
    deltaChanges: [
      { field: 'PackageCode', oldValue: 'Standard', newValue: 'VIP' },
      { field: 'Status', oldValue: 'Draft', newValue: 'Published' },
    ],
  },
]

export const inquiries = [
  { name: 'Hoàng Minh Tuấn', avatar: '/images/avatar-2.png', property: 'Biệt Thự Đơn Lập Thảo Điền', message: 'Tôi muốn hẹn đi xem nhà vào cuối tuần này lúc 9h sáng.', time: '10:59 AM', tag: 'Mới' as const },
  { name: 'Nguyễn Thị Bích', avatar: '/images/avatar-3.png', property: 'Căn Hộ Landmark 81', message: 'Căn này có thương lượng giá thêm không bạn ơi?', time: '09:42 AM', tag: 'Đã phản hồi' as const },
  { name: 'Trần Văn Đức', avatar: '/images/avatar-4.png', property: 'Nhà Phố Kinh Doanh Hoàn Kiếm', message: 'Có thể gửi thêm sổ hồng và giấy phép xây dựng không?', time: '08:15 AM', tag: 'Mới' as const },
]

export const monthlyListings = [
  { month: 'Tháng 1', value: 120 },
  { month: 'Tháng 2', value: 140 },
  { month: 'Tháng 3', value: 130 },
  { month: 'Tháng 4', value: 160 },
  { month: 'Tháng 5', value: 180 },
  { month: 'Tháng 6', value: 190 },
  { month: 'Tháng 7', value: 200 },
  { month: 'Tháng 8', value: 220 },
]

export const team = [
  { name: 'Nguyễn Gia Bảo', role: 'Team Lead & Backend Architect', avatar: '/images/avatar-1.png' },
  { name: 'Phúc Nguyễn', role: 'Full-stack Developer', avatar: '/images/avatar-3.png' },
  { name: 'Thành viên 3', role: 'DevOps & QA Engineer', avatar: '/images/avatar-2.png' },
]

export const testimonials = [
  { name: 'Nguyễn Hải Đăng', date: '20.11.2025', avatar: '/images/avatar-2.png', text: 'PropNest giúp tôi tìm mua được căn hộ ưng ý tại Thảo Điền nhanh chóng, mọi thông tin đều được kiểm duyệt xác thực.' },
  { name: 'Lê Thuỳ Dung', date: '02.01.2026', avatar: '/images/avatar-3.png', text: 'Quy trình thanh toán gói VIP mượt mà, tin đăng của tôi được đẩy lên top và có người chốt cọc chỉ sau 3 ngày.' },
  { name: 'Trần Quang Khải', date: '10.03.2026', avatar: '/images/avatar-1.png', text: 'Hệ thống quản lý tin đăng rất rõ ràng, xem được lịch sử sửa đổi giá và các lần duyệt tin minh bạch.' },
]
