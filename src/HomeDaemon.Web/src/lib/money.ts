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
 * separator, an optional currency symbol and the usual thousands grouping (`1.234,56` or
 * `1,234.56`). At most two decimals are allowed. Returns null when the text is not valid.
 */
export function parseAmountToMinor(value: string): number | null {
  const cleaned = value
    .trim()
    .replace(/[\s\u00a0]/g, '')
    .replace(/[€$£]/g, '')

  if (cleaned === '' || !/^\d+(?:[.,]\d+)*$/.test(cleaned)) {
    return null
  }

  const lastComma = cleaned.lastIndexOf(',')
  const lastDot = cleaned.lastIndexOf('.')
  let whole: string
  let fraction: string

  if (lastComma >= 0 && lastDot >= 0) {
    const decimalIndex = Math.max(lastComma, lastDot)
    whole = cleaned.slice(0, decimalIndex).replace(/[.,]/g, '')
    fraction = cleaned.slice(decimalIndex + 1)
  } else {
    const separatorIndex = Math.max(lastComma, lastDot)
    if (separatorIndex < 0) {
      whole = cleaned
      fraction = ''
    } else {
      whole = cleaned.slice(0, separatorIndex)
      fraction = cleaned.slice(separatorIndex + 1)
    }
  }

  if (!/^\d+$/.test(whole) || !/^\d{0,2}$/.test(fraction)) {
    return null
  }

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
