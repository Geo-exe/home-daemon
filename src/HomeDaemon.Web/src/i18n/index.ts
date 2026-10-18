import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './en.json'

/**
 * The interface ships in English, but every visible string comes from here. Adding a language
 * means adding one resource file and listing it in {@link supportedLanguages}: no component
 * changes are needed.
 */
export const supportedLanguages = ['en'] as const

export type SupportedLanguage = (typeof supportedLanguages)[number]

export const fallbackLanguage: SupportedLanguage = 'en'

const languageStorageKey = 'home-daemon.language'

function resolveInitialLanguage(): SupportedLanguage {
  const stored = globalThis.localStorage?.getItem(languageStorageKey)
  if (stored && (supportedLanguages as readonly string[]).includes(stored)) {
    return stored as SupportedLanguage
  }

  return fallbackLanguage
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
  },
  lng: resolveInitialLanguage(),
  fallbackLng: fallbackLanguage,
  supportedLngs: [...supportedLanguages],
  interpolation: {
    // React escapes interpolated values already.
    escapeValue: false,
  },
  returnNull: false,
})

export default i18n
