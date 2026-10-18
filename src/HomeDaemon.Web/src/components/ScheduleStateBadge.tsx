import { useTranslation } from 'react-i18next'
import type { ChoreScheduleState } from '../api/types'

export function ScheduleStateBadge({ state }: { state: ChoreScheduleState }) {
  const { t } = useTranslation()

  return <span className={`badge badge--${state.toLowerCase()}`}>{t(`chores.state.${state}`)}</span>
}
