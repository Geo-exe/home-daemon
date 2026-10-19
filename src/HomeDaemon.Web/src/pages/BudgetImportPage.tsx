import { useMemo, useState, type ChangeEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { budgetCategoriesQueryKey, getBudgetCategories } from '../api/budget'
import { getHousehold, getMembers, householdQueryKey, membersQueryKey } from '../api/household'
import { useSession } from '../auth/session-context'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useImportBudgetExpenses } from '../hooks/useBudgetMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import {
  buildImportRows,
  detectColumnMapping,
  distinctImportNames,
  emptyColumnMapping,
  importFields,
  requiredImportFields,
  type ColumnMapping,
  type ImportField,
} from '../lib/budget-import'
import { parseCsv, type ParsedCsv } from '../lib/csv'
import { errorMessageKey } from '../lib/errors'
import { formatMinor } from '../lib/money'

const CREATE = '__create__'
const MAX_FILE_BYTES = 5 * 1024 * 1024

const nameKey = (value: string) => value.trim().toLocaleLowerCase()
const pairKey = (category: string, subcategory: string) =>
  `${nameKey(category)}\u001f${nameKey(subcategory)}`

/**
 * Administrator-only bulk import. The file is read and normalized in the browser; the API
 * validates the whole batch again before writing it, so a bad row cannot leave partial data.
 */
export function BudgetImportPage() {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const isOnline = useOnlineStatus()
  const isAdmin = user?.roles.includes('Admin') ?? false

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const categories = useQuery({
    queryKey: budgetCategoriesQueryKey,
    queryFn: ({ signal }) => getBudgetCategories(signal),
  })
  const importExpenses = useImportBudgetExpenses()

  const [fileName, setFileName] = useState<string | null>(null)
  const [csv, setCsv] = useState<ParsedCsv | null>(null)
  const [mapping, setMapping] = useState<ColumnMapping>(emptyColumnMapping)
  const [fileError, setFileError] = useState<string | null>(null)
  const [payerOverrides, setPayerOverrides] = useState<Record<string, string>>({})
  const [categoryOverrides, setCategoryOverrides] = useState<Record<string, string>>({})
  const [subcategoryOverrides, setSubcategoryOverrides] = useState<Record<string, string>>({})
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [result, setResult] = useState<{ imported: number; skipped: number } | null>(null)

  const participants = useMemo(
    () => (members.data ?? []).filter((member) => member.isBudgetParticipant),
    [members.data],
  )
  const rows = useMemo(() => (csv === null ? [] : buildImportRows(csv, mapping)), [csv, mapping])
  const names = useMemo(() => distinctImportNames(rows), [rows])

  // Defaults only save taps: matching is by trimmed, case-insensitive name and every choice
  // can be overridden below.
  const payerResolutions = useMemo(() => {
    const defaults: Record<string, string> = {}

    for (const payer of names.payers) {
      const match = participants.find((member) => nameKey(member.name) === nameKey(payer))
      defaults[nameKey(payer)] = match?.id ?? ''
    }

    return { ...defaults, ...payerOverrides }
  }, [names.payers, participants, payerOverrides])

  const categoryResolutions = useMemo(() => {
    const defaults: Record<string, string> = {}

    for (const category of names.categories) {
      const match = (categories.data ?? []).find((item) => nameKey(item.name) === nameKey(category))
      defaults[nameKey(category)] = match?.id ?? CREATE
    }

    return { ...defaults, ...categoryOverrides }
  }, [names.categories, categories.data, categoryOverrides])

  const subcategoryResolutions = useMemo(() => {
    const defaults: Record<string, string> = {}

    for (const pair of names.subcategories) {
      const categoryId = categoryResolutions[nameKey(pair.category)]
      const category = (categories.data ?? []).find((item) => item.id === categoryId)
      const match = category?.subcategories.find((item) => nameKey(item.name) === nameKey(pair.name))
      defaults[pairKey(pair.category, pair.name)] = match?.id ?? CREATE
    }

    return { ...defaults, ...subcategoryOverrides }
  }, [names.subcategories, categoryResolutions, categories.data, subcategoryOverrides])

  const missingColumns = requiredImportFields.filter((field) => mapping[field] === null)
  const invalidRows = rows.filter((row) => row.issues.length > 0)
  const unresolvedPayers = names.payers.filter((payer) => payerResolutions[nameKey(payer)] === '')
  const missingCategoryResolutions = names.categories.filter(
    (category) => !categoryResolutions[nameKey(category)],
  )
  const missingSubcategoryResolutions = names.subcategories.filter(
    (pair) => !subcategoryResolutions[pairKey(pair.category, pair.name)],
  )

  const dataReady =
    !household.isPending &&
    !members.isPending &&
    !categories.isPending &&
    !household.error &&
    !members.error &&
    !categories.error

  const canImport =
    isOnline &&
    dataReady &&
    csv !== null &&
    rows.length > 0 &&
    missingColumns.length === 0 &&
    invalidRows.length === 0 &&
    unresolvedPayers.length === 0 &&
    missingCategoryResolutions.length === 0 &&
    missingSubcategoryResolutions.length === 0 &&
    !importExpenses.isPending

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) {
      return
    }

    setFileError(null)
    setSubmitError(null)
    setResult(null)
    setPayerOverrides({})
    setCategoryOverrides({})
    setSubcategoryOverrides({})

    if (file.size > MAX_FILE_BYTES) {
      setFileError(t('budget.import.fileTooLarge'))
      return
    }

    try {
      const parsed = parseCsv(await file.text())

      if (parsed.headers.length === 0 || parsed.rows.length === 0) {
        setFileError(t('budget.import.emptyFile'))
        return
      }

      setCsv(parsed)
      setFileName(file.name)
      setMapping(detectColumnMapping(parsed.headers))
    } catch {
      setFileError(t('budget.import.unreadableFile'))
    }
  }

  const setColumn = (field: ImportField, value: string) => {
    setMapping((current) => ({ ...current, [field]: value === '' ? null : Number(value) }))
  }

  const setCategoryResolution = (category: string, value: string) => {
    setCategoryOverrides((current) => ({ ...current, [nameKey(category)]: value }))

    // Subcategories belong to a category: a manual subcategory choice cannot survive a
    // change of the parent category.
    const prefix = `${nameKey(category)}\u001f`
    setSubcategoryOverrides((current) =>
      Object.fromEntries(Object.entries(current).filter(([key]) => !key.startsWith(prefix))),
    )
  }

  const handleImport = async () => {
    if (!canImport) {
      return
    }

    setSubmitError(null)
    setResult(null)

    try {
      const response = await importExpenses.mutateAsync({
        rows: rows.map((row) => ({
          line: row.line,
          date: row.date!,
          amountMinor: row.amountMinor!,
          category: row.category,
          subcategory: row.subcategory === '' ? null : row.subcategory,
          paidByMemberId: payerResolutions[nameKey(row.paidBy)] ?? '',
          description: row.description === '' ? null : row.description,
        })),
        categories: names.categories.map((category) => {
          const value = categoryResolutions[nameKey(category)] ?? CREATE
          return value === CREATE
            ? { sourceName: category, categoryId: null, create: true }
            : { sourceName: category, categoryId: value, create: false }
        }),
        subcategories: names.subcategories.map((pair) => {
          const value = subcategoryResolutions[pairKey(pair.category, pair.name)] ?? CREATE
          return value === CREATE
            ? {
                categorySourceName: pair.category,
                sourceName: pair.name,
                subcategoryId: null,
                create: true,
              }
            : {
                categorySourceName: pair.category,
                sourceName: pair.name,
                subcategoryId: value,
                create: false,
              }
        }),
      })

      setResult(response)
      // Freshly created categories now exist, so let the next preview match them by name.
      setCategoryOverrides({})
      setSubcategoryOverrides({})
    } catch (error) {
      setSubmitError(errorMessageKey(error))
    }
  }

  if (!isAdmin) {
    return (
      <>
        <PageHeader title={t('budget.import.title')} subtitle={t('budget.import.subtitle')} />
        <p className="notice notice--error" role="alert">
          {t('errors.forbidden')}
        </p>
      </>
    )
  }

  return (
    <>
      <PageHeader
        title={t('budget.import.title')}
        subtitle={t('budget.import.subtitle')}
        actions={
          <Link className="button" to="/budget/expenses">
            {t('budget.nav.expenses')}
          </Link>
        }
      />

      <section className="card stack">
        <h2 className="form-section__title">{t('budget.import.chooseFile')}</h2>
        <p className="field__hint">{t('budget.import.fileHint')}</p>
        <input
          className="input"
          type="file"
          accept=".csv,text/csv"
          aria-label={t('budget.import.chooseFile')}
          onChange={(event) => {
            void handleFile(event)
          }}
        />
        {fileName ? <p className="field__hint">{fileName}</p> : null}
        {fileError ? (
          <p className="notice notice--error" role="alert">
            {fileError}
          </p>
        ) : null}
      </section>

      {csv ? (
        <section className="card stack">
          <h2 className="form-section__title">{t('budget.import.mapColumns')}</h2>
          <p className="field__hint">{t('budget.import.mapColumnsHint')}</p>

          <div className="import-columns">
            {importFields.map((field) => (
              <label className="field" key={field}>
                <span className="field__label">
                  {t(`budget.import.field.${field}`)}
                  {requiredImportFields.includes(field) ? ' *' : ''}
                </span>
                <select
                  className="input"
                  value={mapping[field] ?? ''}
                  onChange={(event) => setColumn(field, event.target.value)}
                >
                  <option value="">{t('budget.import.ignoreColumn')}</option>
                  {csv.headers.map((header, index) => (
                    <option key={`${field}-${index}`} value={index}>
                      {header === '' ? t('budget.import.columnNumber', { number: index + 1 }) : header}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>

          {missingColumns.length > 0 ? (
            <p className="notice notice--warning" role="status">
              {t('budget.import.missingColumns', {
                fields: missingColumns.map((field) => t(`budget.import.field.${field}`)).join(', '),
              })}
            </p>
          ) : null}
        </section>
      ) : null}

      {csv && missingColumns.length === 0 ? (
        <>
          <section className="card stack">
            <h2 className="form-section__title">{t('budget.import.mapPeople')}</h2>
            <p className="field__hint">{t('budget.import.mapPeopleHint')}</p>

            {names.payers.length === 0 ? (
              <p className="field__hint">{t('budget.import.noPayers')}</p>
            ) : (
              <div className="import-columns">
                {names.payers.map((payer) => (
                  <label className="field" key={payer}>
                    <span className="field__label">{payer}</span>
                    <select
                      className="input"
                      value={payerResolutions[nameKey(payer)] ?? ''}
                      onChange={(event) =>
                        setPayerOverrides((current) => ({ ...current, [nameKey(payer)]: event.target.value }))
                      }
                    >
                      <option value="">{t('budget.import.selectPerson')}</option>
                      {participants.map((member) => (
                        <option key={member.id} value={member.id}>
                          {member.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}
              </div>
            )}

            {unresolvedPayers.length > 0 ? (
              <p className="field__error" role="alert">
                {t('budget.import.unmatchedPayers', { names: unresolvedPayers.join(', ') })}
              </p>
            ) : null}
          </section>

          <section className="card stack">
            <h2 className="form-section__title">{t('budget.import.mapCategories')}</h2>
            <p className="field__hint">{t('budget.import.mapCategoriesHint')}</p>

            <div className="import-columns">
              {names.categories.map((category) => {
                const value = categoryResolutions[nameKey(category)] ?? CREATE
                return (
                  <label className="field" key={category}>
                    <span className="field__label">{category}</span>
                    <select
                      className="input"
                      value={value}
                      onChange={(event) => setCategoryResolution(category, event.target.value)}
                    >
                      <option value={CREATE}>{t('budget.import.createCategory')}</option>
                      {(categories.data ?? []).map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name}
                        </option>
                      ))}
                    </select>
                  </label>
                )
              })}
            </div>

            {names.subcategories.length > 0 ? (
              <>
                <h3 className="form-section__title">{t('budget.import.mapSubcategories')}</h3>
                <div className="import-columns">
                  {names.subcategories.map((pair) => {
                    const key = pairKey(pair.category, pair.name)
                    const categoryId = categoryResolutions[nameKey(pair.category)]
                    const category = (categories.data ?? []).find((item) => item.id === categoryId)
                    const value = subcategoryResolutions[key] ?? CREATE
                    return (
                      <label className="field" key={key}>
                        <span className="field__label">
                          {pair.category} · {pair.name}
                        </span>
                        <select
                          className="input"
                          value={value}
                          onChange={(event) =>
                            setSubcategoryOverrides((current) => ({
                              ...current,
                              [key]: event.target.value,
                            }))
                          }
                        >
                          <option value={CREATE}>{t('budget.import.createSubcategory')}</option>
                          {(category?.subcategories ?? []).map((item) => (
                            <option key={item.id} value={item.id}>
                              {item.name}
                            </option>
                          ))}
                        </select>
                      </label>
                    )
                  })}
                </div>
              </>
            ) : null}
          </section>

          <section className="card stack">
            <h2 className="form-section__title">{t('budget.import.preview')}</h2>
            <p className="field__hint">
              {t('budget.import.previewCount', {
                count: rows.length,
                invalid: invalidRows.length,
              })}
            </p>

            <ul className="settings-list import-preview">
              {rows.map((row) => {
                const hasIssue = row.issues.length > 0
                return (
                  <li className={`import-row${hasIssue ? ' is-invalid' : ''}`} key={row.line}>
                    <span className="import-row__line">{t('budget.import.line', { line: row.line })}</span>
                    <span className="import-row__main">
                      {row.date ?? '—'} · {row.category}
                      {row.subcategory ? ` · ${row.subcategory}` : ''}
                      {row.description ? ` · ${row.description}` : ''}
                    </span>
                    <span className="import-row__amount">
                      {row.amountMinor === null
                        ? '—'
                        : (formatMinor(row.amountMinor, household.data?.currency ?? 'EUR', i18n.language) ?? '—')}
                    </span>
                    <span className="import-row__payer">{row.paidBy}</span>
                    {hasIssue ? (
                      <span className="import-row__error">
                        {row.issues
                          .map((issue) => t(`budget.import.validation.${issue}`))
                          .join(' ')}
                      </span>
                    ) : null}
                  </li>
                )
              })}
            </ul>

            {invalidRows.length > 0 ? (
              <p className="notice notice--error" role="alert">
                {t('budget.import.invalidRows', { count: invalidRows.length })}
              </p>
            ) : null}

            {submitError ? (
              <p className="notice notice--error" role="alert">
                {t(submitError)}
              </p>
            ) : null}

            {result ? (
              <p className="notice" role="status">
                {t('budget.import.done', { imported: result.imported, skipped: result.skipped })}
              </p>
            ) : null}

            <div className="import-actions">
              <button
                type="button"
                className="button button--primary"
                disabled={!canImport}
                onClick={() => {
                  void handleImport()
                }}
              >
                {importExpenses.isPending
                  ? t('budget.import.importing')
                  : t('budget.import.submit', { count: rows.length })}
              </button>
            </div>
          </section>
        </>
      ) : null}

      {household.isPending || members.isPending || categories.isPending ? (
        <Spinner label={t('app.loading')} />
      ) : null}
      {household.error ? (
        <ErrorState error={household.error} onRetry={() => void household.refetch()} />
      ) : null}
      {members.error ? <ErrorState error={members.error} onRetry={() => void members.refetch()} /> : null}
      {categories.error ? (
        <ErrorState error={categories.error} onRetry={() => void categories.refetch()} />
      ) : null}
    </>
  )
}
