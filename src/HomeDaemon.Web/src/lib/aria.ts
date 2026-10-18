/**
 * Builds the `aria-describedby` value pointing at the hint and error paragraphs a {@link Field}
 * renders, so assistive technology announces both.
 */
export function describedBy(htmlFor: string, hint?: string, error?: string): string | undefined {
  const ids = [
    hint ? `${htmlFor}-hint` : null,
    error ? `${htmlFor}-error` : null,
  ].filter((value): value is string => value !== null)

  return ids.length > 0 ? ids.join(' ') : undefined
}
