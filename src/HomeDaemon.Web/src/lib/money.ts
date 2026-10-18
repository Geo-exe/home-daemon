/**
 * Money helpers. Amounts travel as integer minor units (cents) and are only formatted or
 * parsed at the edges, so no floating-point rounding ever reaches the API.
 */

/** Fallback used when the configured currency code is not one `Intl` understands. */
const defaultLocale = 'en-US'

export function formatMinor(amountMinor: number | null, currency: string, locale: string): string | null {
  if (amountMinor === null) {
    return null
  }

  return new Intl.NumberFormat(locale, { style: 'currency', currency: safeCurrency(currency) }).format(amountMinor / 100)
}

/**
 * Parses a user-typed amount into minor units. Accepts a dot or a comma as the decimal
 * separator and at most two decimals. Returns null when the text is not a valid amount.
 */
export function parseAmountToMinor(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (!/^\d+(\.\d{0,2})?$/.test(normalized)) {
    return null
  }

  const [whole, fraction = ''] = normalized.split('.')
  const minor = Number(whole) * 100 + Number((fraction + '00').slice(0, 2))
  return Number.isSafeInteger(minor) ? minor : null
}

/** Renders minor units back into an editable text value such as `12.50`. */
export function minorToInputValue(amountMinor: number | null): string {
  if (amountMinor === null) {
    return ''
  }

  const whole = Math.trunc(amountMinor / 100)
  const cents = Math.abs(amountMinor % 100)
    .toString()
    .padStart(2, '0')
  return `${whole}.${cents}`
}

function safeCurrency(currency: string): string {
  try {
    // Throws for a code `Intl` does not know, so validate it by trying once.
    new Intl.NumberFormat(defaultLocale, { style: 'currency', currency }).format(0)
    return currency
  } catch {
    return 'EUR'
  }
}
