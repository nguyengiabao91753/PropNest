'use client'

import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import {
  initialHistories,
  initialProperties,
  initialTransactions,
  initialWallet,
  PACKAGES,
  type ListingHistory,
  type ListingStatus,
  type PackageCode,
  type Property,
  type Wallet,
  type WalletTransaction,
  type WorkflowInstance,
  type WorkflowStatus,
} from './data'

export type Role = 'customer' | 'seller' | 'moderator' | 'admin'
export type Session = { name: string; email: string; role: Role; id?: string } | null

type Store = {
  properties: Property[]
  getProperty: (id: string) => Property | undefined
  addProperty: (p: Property) => void
  updateProperty: (id: string, patch: Partial<Property>, note?: string) => void
  deleteProperty: (id: string) => void
  setStatus: (id: string, status: ListingStatus, reason?: string) => void
  submitListing: (id: string) => void
  hideListing: (id: string) => void
  unhideListing: (id: string) => void
  favorites: Set<string>
  toggleFavorite: (id: string) => void
  session: Session
  signIn: (s: NonNullable<Session>) => void
  signOut: () => void
  switchRole: (role: Role) => void

  // Wallet & Monetization
  wallet: Wallet
  transactions: WalletTransaction[]
  topUpWallet: (amount: number) => void

  // Saga Workflow Orchestration
  workflows: Record<string, WorkflowInstance>
  getWorkflow: (correlationId: string) => WorkflowInstance | undefined
  startPurchaseWorkflow: (listingId: string, packageCode: PackageCode, simulateFailure?: boolean) => Promise<string>

  // History Delta
  histories: ListingHistory[]
  getHistories: (listingId: string) => ListingHistory[]

  // Moderation
  moderationApprove: (listingId: string) => void
  moderationReject: (listingId: string, reason: string) => void
}

const StoreContext = createContext<Store | null>(null)

export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [properties, setProperties] = useState<Property[]>(initialProperties)
  const [favorites, setFavorites] = useState<Set<string>>(() => new Set())
  const [session, setSession] = useState<Session>({
    name: 'Nguyễn Văn Minh',
    email: 'minh.nguyen@propnest.vn',
    role: 'seller',
    id: 'seller-1',
  })

  const [wallet, setWallet] = useState<Wallet>(initialWallet)
  const [transactions, setTransactions] = useState<WalletTransaction[]>(initialTransactions)
  const [workflows, setWorkflows] = useState<Record<string, WorkflowInstance>>({})
  const [histories, setHistories] = useState<ListingHistory[]>(initialHistories)

  const getProperty = useCallback((id: string) => properties.find((p) => p.id === id), [properties])

  const addProperty = useCallback((p: Property) => {
    setProperties((prev) => [p, ...prev])
    // Log creation history
    const hist: ListingHistory = {
      historyId: `hist-${Date.now()}`,
      listingId: p.id,
      actor: p.sellerName || 'Seller',
      actionType: 'Create',
      actionDate: new Date().toISOString(),
      note: 'Khởi tạo tin đăng mới',
      deltaChanges: [
        { field: 'Status', oldValue: null, newValue: p.status },
        { field: 'Title', oldValue: null, newValue: p.title },
        { field: 'Price', oldValue: null, newValue: `${p.price.toLocaleString('vi-VN')} ₫` },
      ],
    }
    setHistories((prev) => [hist, ...prev])
  }, [])

  const updateProperty = useCallback((id: string, patch: Partial<Property>, note?: string) => {
    setProperties((prev) => {
      const existing = prev.find((p) => p.id === id)
      if (!existing) return prev

      // Calculate delta changes
      const deltas: { field: string; oldValue: any; newValue: any }[] = []
      Object.keys(patch).forEach((key) => {
        const k = key as keyof Property
        if (existing[k] !== patch[k] && patch[k] !== undefined) {
          deltas.push({ field: key, oldValue: existing[k], newValue: patch[k] })
        }
      })

      if (deltas.length > 0) {
        const hist: ListingHistory = {
          historyId: `hist-${Date.now()}`,
          listingId: id,
          actor: 'Seller / User',
          actionType: 'Update',
          actionDate: new Date().toISOString(),
          note: note || 'Cập nhật thông tin tin đăng',
          deltaChanges: deltas,
        }
        setHistories((h) => [hist, ...h])
      }

      // Optimistic concurrency increment rowVersion
      const nextVersion = `AAAAAA${Date.now().toString().slice(-4)}A=`
      return prev.map((p) => (p.id === id ? { ...p, ...patch, rowVersion: nextVersion } : p))
    })
  }, [])

  const deleteProperty = useCallback((id: string) => setProperties((prev) => prev.filter((p) => p.id !== id)), [])

  const setStatus = useCallback(
    (id: string, status: ListingStatus | string, reason?: string) =>
      setProperties((prev) =>
        prev.map((p) => {
          if (p.id !== id) return p
          const normalized =
            status === 'approved'
              ? 'Published'
              : status === 'pending'
                ? 'PendingModeration'
                : status === 'rejected'
                  ? 'Rejected'
                  : (status as ListingStatus)
          return {
            ...p,
            status: normalized,
            rejectReason: normalized === 'Rejected' ? reason : undefined,
          }
        }),
      ),
    [],
  )

  const submitListing = useCallback((id: string) => {
    setProperties((prev) =>
      prev.map((p) => {
        if (p.id !== id) return p
        return { ...p, status: 'PendingModeration' }
      }),
    )
    const hist: ListingHistory = {
      historyId: `hist-${Date.now()}`,
      listingId: id,
      actor: 'Seller',
      actionType: 'Submit',
      actionDate: new Date().toISOString(),
      note: 'Gửi tin đăng vào quy trình kiểm duyệt',
      deltaChanges: [{ field: 'Status', oldValue: 'Draft', newValue: 'PendingModeration' }],
    }
    setHistories((prev) => [hist, ...prev])
  }, [])

  const hideListing = useCallback((id: string) => {
    setProperties((prev) => prev.map((p) => (p.id === id ? { ...p, status: 'Hidden' } : p)))
  }, [])

  const unhideListing = useCallback((id: string) => {
    setProperties((prev) => prev.map((p) => (p.id === id ? { ...p, status: 'Published' } : p)))
  }, [])

  const toggleFavorite = useCallback(
    (id: string) =>
      setFavorites((prev) => {
        const next = new Set(prev)
        if (next.has(id)) next.delete(id)
        else next.add(id)
        return next
      }),
    [],
  )

  const switchRole = useCallback((role: Role) => {
    setSession((prev) => {
      if (!prev) return null
      return {
        ...prev,
        role,
        name: role === 'moderator' ? 'Kiểm Duyệt Viên PropNest' : role === 'admin' ? 'Quản Trị Viên' : 'Nguyễn Văn Minh',
      }
    })
  }, [])

  // Wallet management
  const topUpWallet = useCallback((amount: number) => {
    setWallet((prev) => {
      const nextMain = prev.mainBalance + amount
      const tx: WalletTransaction = {
        id: `tx-${Date.now()}`,
        correlationId: `topup-${Date.now()}`,
        type: 'Deposit',
        amount,
        balanceBefore: prev.mainBalance + prev.promoBalance,
        balanceAfter: nextMain + prev.promoBalance,
        description: `Nạp tiền tài khoản demo (+${amount.toLocaleString('vi-VN')} ₫)`,
        createdAt: new Date().toISOString(),
      }
      setTransactions((t) => [tx, ...t])
      return { ...prev, mainBalance: nextMain, rowVersion: `AAAAAA${Date.now().toString().slice(-4)}W=` }
    })
  }, [])

  // History queries
  const getHistories = useCallback((listingId: string) => histories.filter((h) => h.listingId === listingId), [histories])
  const getWorkflow = useCallback((correlationId: string) => workflows[correlationId], [workflows])

  // Saga Purchase Workflow
  const startPurchaseWorkflow = useCallback(
    async (listingId: string, packageCode: PackageCode, simulateFailure = false): Promise<string> => {
      const correlationId = crypto.randomUUID ? crypto.randomUUID() : `corr-${Date.now()}`
      const pkg = PACKAGES.find((p) => p.code === packageCode)
      const chargeAmount = pkg ? pkg.price : 0

      // Initial Workflow Step: Started
      const initialWf: WorkflowInstance = {
        correlationId,
        listingId,
        currentState: 'Started',
        packageCode,
        chargeAmount,
        createdAt: new Date().toISOString(),
        steps: [{ name: 'Started (Khởi tạo Saga)', status: 'completed', timestamp: new Date().toISOString() }],
      }

      setWorkflows((prev) => ({ ...prev, [correlationId]: initialWf }))

      // Helper to update workflow step state
      const updateWfStep = (state: WorkflowStatus, stepName: string, status: 'in_progress' | 'completed' | 'failed', error?: string) => {
        setWorkflows((prev) => {
          const current = prev[correlationId] || initialWf
          const existingStepIdx = current.steps.findIndex((s) => s.name.startsWith(stepName))
          let updatedSteps = [...current.steps]
          if (existingStepIdx >= 0) {
            updatedSteps[existingStepIdx] = { ...updatedSteps[existingStepIdx], status, timestamp: new Date().toISOString(), error }
          } else {
            updatedSteps.push({ name: stepName, status, timestamp: new Date().toISOString(), error })
          }
          return {
            ...prev,
            [correlationId]: {
              ...current,
              currentState: state,
              steps: updatedSteps,
              errorMessage: error,
            },
          }
        })
      }

      // Step 1: DeductingWallet
      updateWfStep('DeductingWallet', 'DeductingWallet (Trừ tiền ví)', 'in_progress')
      await new Promise((r) => setTimeout(r, 600))

      // Check balance: promo first, then main
      let promoDeduct = Math.min(wallet.promoBalance, chargeAmount)
      let mainDeduct = chargeAmount - promoDeduct

      if (wallet.mainBalance < mainDeduct) {
        updateWfStep('Failed', 'DeductingWallet (Trừ tiền ví)', 'failed', 'Số dư ví không đủ để thanh toán gói.')
        throw new Error('Số dư ví không đủ. Vui lòng nạp thêm tiền vào tài khoản!')
      }

      // Apply wallet deduction
      const balanceBefore = wallet.mainBalance + wallet.promoBalance
      const nextPromo = wallet.promoBalance - promoDeduct
      const nextMain = wallet.mainBalance - mainDeduct
      const balanceAfter = nextMain + nextPromo

      setWallet((prev) => ({ ...prev, mainBalance: nextMain, promoBalance: nextPromo }))

      const chargeTx: WalletTransaction = {
        id: `tx-${Date.now()}`,
        correlationId,
        type: 'Charge',
        amount: chargeAmount,
        balanceBefore,
        balanceAfter,
        description: `Thanh toán ${pkg?.name || packageCode} cho tin ${listingId}`,
        createdAt: new Date().toISOString(),
      }
      setTransactions((t) => [chargeTx, ...t])
      updateWfStep('WalletDeducted', 'DeductingWallet (Trừ tiền ví)', 'completed')

      // Step 2: UpgradingListing
      updateWfStep('UpgradingListing', 'UpgradingListing (Nâng cấp tin đăng)', 'in_progress')
      await new Promise((r) => setTimeout(r, 800))

      if (simulateFailure) {
        // Demonstrate Compensation Flow (Refund!)
        updateWfStep('Compensating', 'UpgradingListing (Nâng cấp tin đăng)', 'failed', 'Giả lập lỗi database khi nâng cấp tin.')
        await new Promise((r) => setTimeout(r, 800))

        // Execute Compensation: Refund Wallet
        setWallet((prev) => ({ ...prev, mainBalance: prev.mainBalance + mainDeduct, promoBalance: prev.promoBalance + promoDeduct }))
        const refundTx: WalletTransaction = {
          id: `tx-refund-${Date.now()}`,
          correlationId,
          type: 'Refund',
          amount: chargeAmount,
          balanceBefore: balanceAfter,
          balanceAfter: balanceAfter + chargeAmount,
          description: `[Bồi hoàn Saga] Hoàn tiền thanh toán lỗi cho tin ${listingId}`,
          createdAt: new Date().toISOString(),
        }
        setTransactions((t) => [refundTx, ...t])

        updateWfStep('Compensated', 'Compensating (Hoàn tiền bồi hoàn)', 'completed')
        throw new Error('Giao dịch nâng cấp thất bại! Hệ thống Saga đã kích hoạt Compensation hoàn tiền về ví cho bạn.')
      }

      // Success Path
      setProperties((prev) =>
        prev.map((p) => {
          if (p.id !== listingId) return p
          return {
            ...p,
            packageCode,
            status: 'Published',
            featured: packageCode === 'VIP',
            banner: packageCode === 'VIP' ? 'VIP Nổi Bật' : packageCode === 'Boost' ? 'Hot Listing' : undefined,
          }
        }),
      )
      updateWfStep('RecordingHistory', 'UpgradingListing (Nâng cấp tin đăng)', 'completed')

      // Step 3: Record Delta History
      const hist: ListingHistory = {
        historyId: `hist-${Date.now()}`,
        listingId,
        actor: 'Saga Orchestrator',
        actionType: 'UpgradePackage',
        actionDate: new Date().toISOString(),
        note: `Nâng cấp ${pkg?.name || packageCode} thành công qua MassTransit Saga (Correlation: ${correlationId})`,
        deltaChanges: [
          { field: 'PackageCode', oldValue: 'Standard', newValue: packageCode },
          { field: 'Status', oldValue: 'PendingPayment', newValue: 'Published' },
        ],
      }
      setHistories((h) => [hist, ...h])

      // Step 4: Completed
      updateWfStep('Completed', 'WorkflowCompleted (Hoàn tất giao dịch)', 'completed')

      return correlationId
    },
    [wallet],
  )

  // Moderation actions
  const moderationApprove = useCallback((listingId: string) => {
    setProperties((prev) => prev.map((p) => (p.id === listingId ? { ...p, status: 'Published', rejectReason: undefined } : p)))
    const hist: ListingHistory = {
      historyId: `hist-${Date.now()}`,
      listingId,
      actor: 'Kiểm Duyệt Viên PropNest',
      actionType: 'Approve',
      actionDate: new Date().toISOString(),
      note: 'Tin đăng đạt chuẩn kiểm duyệt và được phê duyệt xuất bản',
      deltaChanges: [{ field: 'Status', oldValue: 'PendingModeration', newValue: 'Published' }],
    }
    setHistories((h) => [hist, ...h])
  }, [])

  const moderationReject = useCallback((listingId: string, reason: string) => {
    setProperties((prev) => prev.map((p) => (p.id === listingId ? { ...p, status: 'Rejected', rejectReason: reason } : p)))
    const hist: ListingHistory = {
      historyId: `hist-${Date.now()}`,
      listingId,
      actor: 'Kiểm Duyệt Viên PropNest',
      actionType: 'Reject',
      actionDate: new Date().toISOString(),
      note: `Từ chối tin đăng: ${reason}`,
      deltaChanges: [{ field: 'Status', oldValue: 'PendingModeration', newValue: 'Rejected' }],
    }
    setHistories((h) => [hist, ...h])
  }, [])

  const value = useMemo<Store>(
    () => ({
      properties,
      getProperty,
      addProperty,
      updateProperty,
      deleteProperty,
      setStatus,
      submitListing,
      hideListing,
      unhideListing,
      favorites,
      toggleFavorite,
      session,
      signIn: (s: NonNullable<Session>) =>
        setSession({
          id: s.id || (s.role === 'seller' ? 'seller-1' : s.role === 'admin' ? 'admin-1' : 'customer-1'),
          name: s.name,
          email: s.email,
          role: s.role,
        }),
      signOut: () => setSession(null),
      switchRole,
      wallet,
      transactions,
      topUpWallet,
      workflows,
      getWorkflow,
      startPurchaseWorkflow,
      histories,
      getHistories,
      moderationApprove,
      moderationReject,
    }),
    [
      properties,
      getProperty,
      addProperty,
      updateProperty,
      deleteProperty,
      setStatus,
      submitListing,
      hideListing,
      unhideListing,
      favorites,
      toggleFavorite,
      session,
      switchRole,
      wallet,
      transactions,
      topUpWallet,
      workflows,
      getWorkflow,
      startPurchaseWorkflow,
      histories,
      getHistories,
      moderationApprove,
      moderationReject,
    ],
  )

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
}

export function useStore() {
  const ctx = useContext(StoreContext)
  if (!ctx) throw new Error('useStore must be used within StoreProvider')
  return ctx
}
