import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Бекенд у розробці слухає http://localhost:5080 (профіль "http" AutoLot.Api).
// Проксі тримає фронт і API на одному походженні, тож CORS у dev не заважає.
const backendUrl = process.env.VITE_BACKEND_URL ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],

  build: {
    rollupOptions: {
      output: {
        /*
          Свій код і чужі бібліотеки — в різні файли.

          Збирач і сам ділить бандл на шматки, але кладе React упереміш із
          нашими сторінками. Тоді будь-яка правка в коді змінює ім'я файла,
          у якому лежить ще й React, — і браузер вантажить заново все, хоча
          бібліотека не змінилася ані на байт.

          Тут ми відкладаємо вміст node_modules в окремий шматок: він має
          власне ім'я з власним відбитком і після кожного оновлення сайту
          лишається в кеші браузера незайманим.

          codeSplitting — налаштування Rolldown, збирача, на якому працює
          Vite 8. groups — перелік груп; test — регулярний вираз, який
          звіряють зі шляхом модуля. Роздільників у ньому немає навмисно:
          у Windows шлях іде через «\», у Linux через «/», а назва теки
          однакова скрізь — її й шукаємо.
        */
        codeSplitting: {
          groups: [{ name: 'vendor', test: /node_modules/ }],
        },
      },
    },
  },
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
