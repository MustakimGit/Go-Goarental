import { useEffect } from 'react'
import { useDispatch } from 'react-redux'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { Toaster } from 'sonner'
import { fetchCurrentUser } from '@/features/auth/authSlice'
import RequireAuth from '@/features/auth/RequireAuth'
import SearchPage from '@/features/search/SearchPage'
import CustomerLayout from '@/layouts/CustomerLayout'
import HomePage from '@/pages/HomePage'
import NotFoundPage from '@/pages/NotFoundPage'

// Forms and signed-in areas load on demand so the home and search pages stay light.
const page = (load) => () => load().then((m) => ({ Component: m.default }))

const router = createBrowserRouter([
  {
    element: <CustomerLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', lazy: page(() => import('@/features/auth/LoginPage')) },
      { path: 'register', lazy: page(() => import('@/features/auth/RegisterPage')) },
      { path: 'search', element: <SearchPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: 'account', lazy: page(() => import('@/pages/AccountPage')) }],
      },
      {
        path: 'partner',
        element: <RequireAuth roles={['Partner']} />,
        children: [
          { path: 'vehicles', lazy: page(() => import('@/features/partners/VehiclesPage')) },
          { path: 'vehicles/:id', lazy: page(() => import('@/features/partners/VehicleDetailPage')) },
        ],
      },
      {
        path: 'admin',
        element: <RequireAuth roles={['Admin']} />,
        children: [
          { path: 'dashboard', lazy: page(() => import('@/features/admin/DashboardPage')) },
          { path: 'partners', lazy: page(() => import('@/features/admin/PartnersPage')) },
          { path: 'vehicles', lazy: page(() => import('@/features/admin/VehicleReviewPage')) },
          { path: 'catalog', lazy: page(() => import('@/features/admin/CatalogPage')) },
          { path: 'pricing', lazy: page(() => import('@/features/admin/PricingPage')) },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

export default function App() {
  const dispatch = useDispatch()

  useEffect(() => {
    dispatch(fetchCurrentUser())
  }, [dispatch])

  return (
    <>
      <RouterProvider router={router} />
      <Toaster richColors position="top-right" />
    </>
  )
}
