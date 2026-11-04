import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: { chunkSizeWarningLimit: 1000 },
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  server: {
    port: 5173,
    // In development the API runs separately; proxy /api so the browser sees one origin.
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080', changeOrigin: true },
    },
  },
})
