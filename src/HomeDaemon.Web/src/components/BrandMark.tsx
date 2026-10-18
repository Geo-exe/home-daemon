/**
 * The HomeDaemon mark: the little daemon that lives in the house, matching the app icon. It is
 * inline so it stays crisp at any size and needs no extra request.
 */
export function BrandMark({ size = 32 }: { size?: number }) {
  return (
    <svg
      className="app__brand-mark"
      viewBox="0 0 512 512"
      width={size}
      height={size}
      aria-hidden="true"
      focusable="false"
    >
      <rect width="512" height="512" rx="112" fill="currentColor" />
      <g fill="none" stroke="#ffffff" strokeWidth={32} strokeLinecap="round" strokeLinejoin="round">
        <path d="M128 248 L256 144 L384 248" />
        <path d="M168 248 L168 372 L344 372 L344 248" />
      </g>
      <g fill="#ffffff">
        <path d="M240 270 L222 290 L208 232 Z" />
        <path d="M272 270 L290 290 L304 232 Z" />
        <circle cx="256" cy="308" r="42" />
      </g>
      <g fill="#4f46e5">
        <circle cx="240" cy="300" r="8" />
        <circle cx="272" cy="300" r="8" />
      </g>
      <path d="M238 328 q18 14 36 0" fill="none" stroke="#4f46e5" strokeWidth={9} strokeLinecap="round" />
    </svg>
  )
}
