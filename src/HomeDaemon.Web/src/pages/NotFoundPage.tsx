import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/PageHeader'

export function NotFoundPage() {
  const { t } = useTranslation()

  return (
    <>
      <PageHeader title={t('errors.notFound')} />
      <Link className="button" to="/chores">
        {t('chores.detail.backToList')}
      </Link>
    </>
  )
}
