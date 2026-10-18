import { useEffect, useRef } from 'react'

/**
 * Keeps a native `<dialog>` element in sync with a boolean. jsdom (used by the tests) does not
 * implement `showModal`, so the attribute is used as a fallback.
 */
export function useDialogElement(open: boolean) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (dialog === null) {
      return
    }

    if (open && !dialog.open) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal()
      } else {
        dialog.setAttribute('open', '')
      }
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [open])

  return ref
}
