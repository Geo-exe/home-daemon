import { useEffect, useState } from 'react'

/**
 * Tracks connectivity. The first version does not queue writes, so the UI disables actions
 * while offline instead of pretending they were saved.
 */
export function useOnlineStatus(): boolean {
  const [isOnline, setIsOnline] = useState(() => globalThis.navigator?.onLine ?? true)

  useEffect(() => {
    const goOnline = () => setIsOnline(true)
    const goOffline = () => setIsOnline(false)

    globalThis.addEventListener('online', goOnline)
    globalThis.addEventListener('offline', goOffline)

    return () => {
      globalThis.removeEventListener('online', goOnline)
      globalThis.removeEventListener('offline', goOffline)
    }
  }, [])

  return isOnline
}
