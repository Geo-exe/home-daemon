import type { BudgetTransferSuggestion } from '../api/types'

/**
 * The two directions to offer first in the transfer dialog. Recent history comes first, then
 * the participants are walked in their stable (name) order so a household with no history
 * still gets suggestions.
 */
export function buildTransferShortcuts(
  participants: { id: string; name: string }[],
  suggestions: BudgetTransferSuggestion[],
): BudgetTransferSuggestion[] {
  const byId = new Map(participants.map((member) => [member.id, member]))
  const seen = new Set<string>()
  const result: BudgetTransferSuggestion[] = []

  const add = (fromMemberId: string, toMemberId: string) => {
    if (fromMemberId === toMemberId || seen.has(`${fromMemberId}-${toMemberId}`)) {
      return
    }

    const from = byId.get(fromMemberId)
    const to = byId.get(toMemberId)
    if (!from || !to) {
      return
    }

    seen.add(`${fromMemberId}-${toMemberId}`)
    result.push({
      fromMemberId: from.id,
      fromMemberName: from.name,
      toMemberId: to.id,
      toMemberName: to.name,
    })
  }

  for (const suggestion of suggestions) {
    add(suggestion.fromMemberId, suggestion.toMemberId)
    if (result.length >= 2) {
      return result
    }
  }

  for (const from of participants) {
    for (const to of participants) {
      add(from.id, to.id)
      if (result.length >= 2) {
        return result
      }
    }
  }

  return result
}
