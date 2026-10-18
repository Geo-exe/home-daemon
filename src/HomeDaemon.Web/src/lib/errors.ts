import { ApiError } from '../api/client'

/**
 * Maps a failure to an i18n key. Field-level problems returned by the API are kept separate so
 * forms can attach them to the right input.
 */
export function errorMessageKey(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isUnauthorized) {
      return 'errors.sessionExpired'
    }

    if (error.isForbidden) {
      return 'errors.forbidden'
    }

    if (error.isNotFound) {
      return 'errors.notFound'
    }

    if (error.isConflict) {
      return 'errors.conflict'
    }

    return 'errors.generic'
  }

  // A fetch that never reached the server rejects with a TypeError.
  if (error instanceof TypeError) {
    return 'errors.offline'
  }

  return 'errors.generic'
}

export function fieldProblems(error: unknown): Record<string, string[]> {
  return error instanceof ApiError ? (error.problems ?? {}) : {}
}

export function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.isNotFound
}
