import { lazy } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { SiteLayout } from './components/SiteLayout'
import { CatalogPage } from './pages/CatalogPage'

/*
  Усі сторінки, крім каталогу, вантажаться окремими шматками.

  Навіщо. Досі браузер завантажував увесь застосунок одним файлом на
  півмегабайта: людина, яка зайшла подивитися одне авто, отримувала разом
  із ним адмінку, форму подання оголошення, порівняння й бібліотеку
  реального часу для торгів і чату. Усе це їй того разу не знадобиться.

  Каталог лишається звичайним імпортом навмисно: це перше, що бачить
  відвідувач, і відкладати його означало б додати зайвий похід у мережу
  саме там, де важлива швидкість.

  lazy() бере функцію, яка ПОЧИНАЄ завантаження лише тоді, коли сторінка
  справді знадобилася. Поки шматок летить, React показує запасний вміст —
  його задає Suspense усередині SiteLayout, тож шапка лишається на місці.
*/
const AccountPage = lazy(async () => ({ default: (await import('./pages/AccountPage')).AccountPage }))
const ChatPage = lazy(async () => ({ default: (await import('./pages/ChatPage')).ChatPage }))
const ComparePage = lazy(async () => ({ default: (await import('./pages/ComparePage')).ComparePage }))
const ConfirmEmailPage = lazy(async () => ({
  default: (await import('./pages/ConfirmEmailPage')).ConfirmEmailPage,
}))
const DealershipPage = lazy(async () => ({
  default: (await import('./pages/DealershipPage')).DealershipPage,
}))
const DealershipsPage = lazy(async () => ({
  default: (await import('./pages/DealershipsPage')).DealershipsPage,
}))
const FavoritesPage = lazy(async () => ({
  default: (await import('./pages/FavoritesPage')).FavoritesPage,
}))
const ListingFormPage = lazy(async () => ({
  default: (await import('./pages/ListingFormPage')).ListingFormPage,
}))
const ListingPage = lazy(async () => ({ default: (await import('./pages/ListingPage')).ListingPage }))
const MyDealershipPage = lazy(async () => ({
  default: (await import('./pages/MyDealershipPage')).MyDealershipPage,
}))
const ResetPasswordPage = lazy(async () => ({
  default: (await import('./pages/ResetPasswordPage')).ResetPasswordPage,
}))
const UserProfilePage = lazy(async () => ({
  default: (await import('./pages/UserProfilePage')).UserProfilePage,
}))
const ViewedPage = lazy(async () => ({ default: (await import('./pages/ViewedPage')).ViewedPage }))

// Адмінка — окремий шматок на весь розділ: хто в неї заходить, той заходить
// у неї цілком, а решта не завантажує жодного її екрана.
const AdminLayout = lazy(async () => ({
  default: (await import('./pages/admin/AdminLayout')).AdminLayout,
}))
const AdminOverviewPage = lazy(async () => ({
  default: (await import('./pages/admin/AdminOverviewPage')).AdminOverviewPage,
}))
const AdminUsersPage = lazy(async () => ({
  default: (await import('./pages/admin/AdminUsersPage')).AdminUsersPage,
}))
const ModerationQueuePage = lazy(async () => ({
  default: (await import('./pages/admin/ModerationQueuePage')).ModerationQueuePage,
}))
const DealershipQueuePage = lazy(async () => ({
  default: (await import('./pages/admin/DealershipQueuePage')).DealershipQueuePage,
}))
const ReportQueuePage = lazy(async () => ({
  default: (await import('./pages/admin/ReportQueuePage')).ReportQueuePage,
}))

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<SiteLayout />}>
            <Route index element={<CatalogPage />} />
            <Route path="listing/:id" element={<ListingPage />} />

            {/*
              Подання й редагування — одна сторінка за двома адресами.
              «sell» без ідентифікатора читається як дія, а не як розділ.
            */}
            <Route path="sell" element={<ListingFormPage />} />
            <Route path="listing/:id/edit" element={<ListingFormPage />} />
            <Route path="favorites" element={<FavoritesPage />} />
            <Route path="viewed" element={<ViewedPage />} />
            <Route path="compare" element={<ComparePage />} />
            <Route path="account" element={<AccountPage />} />
            <Route path="chat" element={<ChatPage />} />
            <Route path="dealers" element={<DealershipsPage />} />
            <Route path="dealers/:slug" element={<DealershipPage />} />

            {/*
              Керування салоном свідомо ПОЗА «dealers/…»: там живуть вітрини
              за назвою, і «dealers/manage» збігся б із салоном, чий адресний
              рядок хтось назвав саме так.
            */}
            <Route path="my-dealership" element={<MyDealershipPage />} />
            <Route path="users/:id" element={<UserProfilePage />} />
            <Route path="reset-password" element={<ResetPasswordPage />} />
            <Route path="confirm-email" element={<ConfirmEmailPage />} />

            <Route path="admin" element={<AdminLayout />}>
              <Route index element={<AdminOverviewPage />} />
              <Route path="queue" element={<ModerationQueuePage />} />
              <Route path="reports" element={<ReportQueuePage />} />
              <Route path="dealerships" element={<DealershipQueuePage />} />
              <Route path="users" element={<AdminUsersPage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
