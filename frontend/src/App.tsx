import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { SiteLayout } from './components/SiteLayout'
import { CatalogPage } from './pages/CatalogPage'
import { ListingPage } from './pages/ListingPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<SiteLayout />}>
          <Route index element={<CatalogPage />} />
          <Route path="listing/:id" element={<ListingPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
