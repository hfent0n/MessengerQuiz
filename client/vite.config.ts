import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// API_URL lets a second copy of the app run against a second API, such as during testing.
const apiUrl = process.env.API_URL ?? 'http://localhost:5256'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // ws lets the multiplayer game's SignalR connection use WebSockets through the proxy.
      '/api': { target: apiUrl, ws: true },
    },
  },
})
