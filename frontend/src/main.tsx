import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import App from './App'
import { subscribeToLanguage } from './i18n/languageStore'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
})

/*
  Довідники, назви міст і кузовів приходять із сервера вже перекладеними —
  отже, зміна мови робить УВЕСЬ кеш запитів застарілим. invalidateQueries
  помічає його недійсним: те, що зараз на екрані, перезапитується одразу,
  решта — коли знадобиться. Без цього людина перемкнула б мову й далі
  бачила старі назви, доки кеш не протух би сам.
*/
subscribeToLanguage(() => void queryClient.invalidateQueries())

const container = document.getElementById('root')

if (!container) {
  throw new Error('Не знайдено кореневий елемент #root')
}

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
