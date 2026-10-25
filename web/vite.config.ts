/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // In development the browser talks to one server (Vite), which forwards /api/* to the .NET API.
    // Same origin means no CORS setup. /api is stripped, so /api/scans reaches the API's /scans.
    // The dashboard's own pages use /scans/{id} too, which is why the API calls get a prefix.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    environmentOptions: { jsdom: { url: 'http://localhost/' } },
    setupFiles: ['./src/test/setup.ts'],
  },
})
