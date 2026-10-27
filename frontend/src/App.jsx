import { useCallback, useEffect, useState } from 'react'
import AppHeader from './components/AppHeader'
import StockTable from './components/StockTable'
import TransferForm from './components/TransferForm'
import TransferHistory from './components/TransferHistory'
import {
  getAllBranchStocks,
  getBranches,
  getProducts,
  getTransfers,
} from './services/inventoryApi'
import './App.css'

export default function App() {
  const [branches, setBranches] = useState([])
  const [products, setProducts] = useState([])
  const [stockByBranch, setStockByBranch] = useState({})
  const [transfers, setTransfers] = useState([])

  const [isLoadingStock, setIsLoadingStock] = useState(true)
  const [isLoadingHistory, setIsLoadingHistory] = useState(true)

  const [stockError, setStockError] = useState(null)
  const [historyError, setHistoryError] = useState(null)

  // Reusable stock fetcher for user-triggered refreshes and post-transfer updates
  const fetchStock = useCallback(async (signal) => {
    setIsLoadingStock(true)
    setStockError(null)

    try {
      const [fetchedBranches, fetchedProducts] = await Promise.all([
        getBranches(signal),
        getProducts(signal),
      ])

      setBranches(fetchedBranches)
      setProducts(fetchedProducts)

      const branchStocks = await getAllBranchStocks(fetchedBranches, signal)

      const stockMap = {}
      for (const bs of branchStocks) {
        if (bs && bs.branchId && Array.isArray(bs.items)) {
          stockMap[bs.branchId] = {}
          for (const item of bs.items) {
            stockMap[bs.branchId][item.productId] = item.quantity
          }
        }
      }

      setStockByBranch(stockMap)
    } catch (err) {
      if (err.name !== 'AbortError') {
        setStockError(err.message || 'Unable to load branch stock.')
      }
      throw err
    } finally {
      setIsLoadingStock(false)
    }
  }, [])

  // Reusable history fetcher for user-triggered refreshes and post-transfer updates
  const fetchHistory = useCallback(async (signal) => {
    setIsLoadingHistory(true)
    setHistoryError(null)

    try {
      const history = await getTransfers(signal)
      setTransfers(history)
    } catch (err) {
      if (err.name !== 'AbortError') {
        setHistoryError(err.message || 'Unable to load transfer history.')
      }
      throw err
    } finally {
      setIsLoadingHistory(false)
    }
  }, [])

  // Initial data load effect
  useEffect(() => {
    let ignore = false
    const controller = new AbortController()

    async function initialLoad() {
      try {
        const [fetchedBranches, fetchedProducts] = await Promise.all([
          getBranches(controller.signal),
          getProducts(controller.signal),
        ])

        if (ignore) return

        setBranches(fetchedBranches)
        setProducts(fetchedProducts)

        const branchStocks = await getAllBranchStocks(fetchedBranches, controller.signal)
        if (ignore) return

        const stockMap = {}
        for (const bs of branchStocks) {
          if (bs && bs.branchId && Array.isArray(bs.items)) {
            stockMap[bs.branchId] = {}
            for (const item of bs.items) {
              stockMap[bs.branchId][item.productId] = item.quantity
            }
          }
        }

        setStockByBranch(stockMap)
        setStockError(null)
      } catch (err) {
        if (!ignore && err.name !== 'AbortError') {
          setStockError(err.message || 'Unable to load branch stock.')
        }
      } finally {
        if (!ignore) {
          setIsLoadingStock(false)
        }
      }

      try {
        const history = await getTransfers(controller.signal)
        if (!ignore) {
          setTransfers(history)
          setHistoryError(null)
        }
      } catch (err) {
        if (!ignore && err.name !== 'AbortError') {
          setHistoryError(err.message || 'Unable to load transfer history.')
        }
      } finally {
        if (!ignore) {
          setIsLoadingHistory(false)
        }
      }
    }

    initialLoad()

    return () => {
      ignore = true
      controller.abort()
    }
  }, [])

  // User-triggered manual refresh
  const handleRefresh = useCallback(async () => {
    await Promise.allSettled([fetchStock(), fetchHistory()])
  }, [fetchStock, fetchHistory])

  // Handles post-transfer refresh; rethrows on refresh failure so TransferForm
  // can notify the user without retracting the transfer confirmation.
  const handleTransferSuccess = async () => {
    await Promise.all([fetchStock(), fetchHistory()])
  }

  return (
    <div className="app-layout">
      <AppHeader />

      <main className="app-main">
        <div className="container">
          <header className="page-header">
            <h1 className="page-title">Stock transfers</h1>
            <p className="page-description">
              View branch stock and move products between stores.
            </p>
          </header>

          {/* Section 1: Branch stock */}
          <StockTable
            branches={branches}
            products={products}
            stockByBranch={stockByBranch}
            isLoading={isLoadingStock}
            error={stockError}
            onRefresh={handleRefresh}
          />

          {/* Section 2: New transfer */}
          <TransferForm
            branches={branches}
            products={products}
            stockByBranch={stockByBranch}
            onTransferSuccess={handleTransferSuccess}
          />

          {/* Section 3: Transfer history */}
          <TransferHistory
            transfers={transfers}
            isLoading={isLoadingHistory}
            error={historyError}
          />
        </div>
      </main>
    </div>
  )
}
