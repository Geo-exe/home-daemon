import { apiGet } from './client'
import type { HomeAssistantEntities } from './types'

export const homeAssistantEntitiesQueryKey = ['integrations', 'home-assistant', 'entities'] as const

/** Lists the entities the settings pickers can offer; the token stays on the server. */
export function getHomeAssistantEntities(signal?: AbortSignal): Promise<HomeAssistantEntities> {
  return apiGet<HomeAssistantEntities>('/integrations/home-assistant/entities', signal)
}
