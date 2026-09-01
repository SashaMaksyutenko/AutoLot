import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { SiteLayout } from './components/SiteLayout'
import { AccountPage } from './pages/AccountPage'
import { CatalogPage } from './pages/CatalogPage'
import { ChatPage } from './pages/ChatPage'
import { AdminLayout } from './pages/admin/AdminLayout'
import { AdminOverviewPage } from './pages/admin/AdminOverviewPage'
import { AdminUsersPage } from './pages/admin/AdminUsersPage'
import { ModerationQueuePage } from './pages/admin/ModerationQueuePage'
import { ReportQueuePage } from './pages/admin/ReportQueuePage'
import { DealershipPage } from './pages/DealershipPage'
import { DealershipsPage } from './pages/DealershipsPage'
import { ConfirmEmailPage } from './pages/ConfirmEmailPage'
import { FavoritesPage } from './pages/FavoritesPage'
import { ListingPage } from './pages/ListingPage'
import { ResetPasswordPage } from './pages/ResetPasswordPage'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<SiteLayout />}>
            <Route index element={<CatalogPage />} />
            <Route path="listing/:id" element={<ListingPage />} />
            <Route path="favorites" element={<FavoritesPage />} />
            <Route path="account" element={<AccountPage />} />
            <Route path="chat" element={<ChatPage />} />
            <Route path="dealers" element={<DealershipsPage />} />
            <Route path="dealers/:slug" element={<DealershipPage />} />
            <Route path="reset-password" element={<ResetPasswordPage />} />
            <Route path="confirm-email" element={<ConfirmEmailPage />} />

            <Route path="admin" element={<AdminLayout />}>
              <Route index element={<AdminOverviewPage />} />
              <Route path="queue" element={<ModerationQueuePage />} />
              <Route path="reports" element={<ReportQueuePage />} />
              <Route path="users" element={<AdminUsersPage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
