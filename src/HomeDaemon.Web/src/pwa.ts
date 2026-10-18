/**
 * Registers the service worker in production builds only. The generated worker caches the app
 * shell and static assets; it never caches API responses, so nothing is served stale as if it
 * were live data.
 */
export function registerServiceWorker(): void {
  if (!import.meta.env.PROD) {
    return
  }

  void import('virtual:pwa-register')
    .then(({ registerSW }) => registerSW({ immediate: true }))
    .catch(() => {
      // A missing service worker must not break the app.
    })
}
