import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { SiteLayout } from './components/SiteLayout'
import { CatalogPage } from './pages/CatalogPage'
import { DealershipPage } from './pages/DealershipPage'
import { DealershipsPage } from './pages/DealershipsPage'
import { FavoritesPage } from './pages/FavoritesPage'
import { ListingPage } from './pages/ListingPage'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<SiteLayout />}>
            <Route index element={<CatalogPage />} />
            <Route path="listing/:id" element={<ListingPage />} />
            <Route path="favorites" element={<FavoritesPage />} />
            <Route path="dealers" element={<DealershipsPage />} />
            <Route path="dealers/:slug" element={<DealershipPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
