import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// The API is served from the same origin in production. During development Vite proxies
// /api and /health to the locally running ASP.NET host, so cookies stay first-party.
const apiTarget = process.env.HOMEDAEMON_API_URL ?? 'http://127.0.0.1:5255'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['icons/icon.svg', 'icons/apple-touch-icon.png'],
      manifest: {
        name: 'HomeDaemon',
        short_name: 'HomeDaemon',
        description: 'Household chores planner',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#0f172a',
        theme_color: '#4f46e5',
        icons: [
          { src: '/icons/icon.svg', sizes: 'any', type: 'image/svg+xml' },
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // The service worker caches the app shell and static assets only. API responses are
        // never cached: the first version is online-only for anything that writes.
        globPatterns: ['**/*.{js,css,html,svg,png,ico,webmanifest}'],
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//, /^\/health/],
        runtimeCaching: [],
        cleanupOutdatedCaches: true,
      },
      devOptions: { enabled: false },
    }),
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false },
      '/health': { target: apiTarget, changeOrigin: false },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
