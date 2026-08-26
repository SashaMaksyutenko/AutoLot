import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Бекенд у розробці слухає http://localhost:5080 (профіль "http" AutoLot.Api).
// Проксі тримає фронт і API на одному походженні, тож CORS у dev не заважає.
const backendUrl = process.env.VITE_BACKEND_URL ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: backendUrl, changeOrigin: true },
      '/health': { target: backendUrl, changeOrigin: true },

      // Фото оголошень роздає бекенд із теки поза wwwroot.
      '/media': { target: backendUrl, changeOrigin: true },

      // Живий канал торгів. ws: true обов'язковий: SignalR починає зі
      // звичайного HTTP, а потім просить браузер «підвищити» з'єднання до
      // WebSocket. Без цього прапорця проксі таке підвищення не пропустить,
      // і канал мовчки відкотиться до повільнішого способу зв'язку.
      '/hubs': { target: backendUrl, changeOrigin: true, ws: true },
    },
  },
})
