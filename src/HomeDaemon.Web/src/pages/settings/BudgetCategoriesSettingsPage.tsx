import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { budgetCategoriesQueryKey, getBudgetCategories } from '../../api/budget'
import type { BudgetCategory, BudgetSubcategory } from '../../api/types'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { Field } from '../../components/Field'
import { FormDialog } from '../../components/FormDialog'
import { PageHeader } from '../../components/PageHeader'
import { Spinner } from '../../components/Spinner'
import {
  useCreateBudgetCategory,
  useCreateBudgetSubcategory,
  useDeleteBudgetCategory,
  useDeleteBudgetSubcategory,
  useUpdateBudgetCategory,
  useUpdateBudgetSubcategory,
} from '../../hooks/useBudgetMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../../lib/errors'
import {
  budgetCategoryFormSchema,
  issuesToFieldErrors,
  type BudgetCategoryFormValues,
} from '../../lib/validation'

export function BudgetCategoriesSettingsPage() {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()

  const categories = useQuery({ queryKey: budgetCategoriesQueryKey, queryFn: ({ signal }) => getBudgetCategories(signal) })
  const removeCategory = useDeleteBudgetCategory()
  const removeSubcategory = useDeleteBudgetSubcategory()

  const [editingCategory, setEditingCategory] = useState<{ open: boolean; category: BudgetCategory | null }>({
    open: false,
    category: null,
  })
  const [editingSubcategory, setEditingSubcategory] = useState<{
    open: boolean
    category: BudgetCategory
    subcategory: BudgetSubcategory | null
  } | null>(null)
  const [pendingCategory, setPendingCategory] = useState<BudgetCategory | null>(null)
  const [pendingSubcategory, setPendingSubcategory] = useState<BudgetSubcategory | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const runDelete = async (action: () => Promise<unknown>) => {
    setErrorKey(null)
    try {
      await action()
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <>
      <PageHeader
        title={t('budget.categories.title')}
        subtitle={t('budget.categories.subtitle')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => setEditingCategory({ open: true, category: null })}
            disabled={!isOnline}
          >
            {t('budget.categories.addCategory')}
          </button>
        }
      />

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      {categories.isPending ? <Spinner label={t('app.loading')} /> : null}
      {categories.error ? <ErrorState error={categories.error} onRetry={() => void categories.refetch()} /> : null}

      {categories.data && categories.data.length === 0 ? <EmptyState message={t('budget.categories.empty')} /> : null}

      {categories.data && categories.data.length > 0 ? (
        <ul className="budget-tree">
          {categories.data.map((category) => (
            <li className="budget-tree__category" key={category.id}>
              <div className="budget-tree__head">
                <div className="budget-tree__identity">
                  <span className="budget-tree__name">{category.name}</span>
                  <span className="budget-tree__meta">
                    {t('budget.categories.summary', {
                      count: category.subcategories.length,
                      order: category.sortOrder,
                    })}
                  </span>
                </div>
                <div className="budget-tree__actions">
                  <button
                    type="button"
                    className="button button--ghost"
                    onClick={() => setEditingSubcategory({ open: true, category, subcategory: null })}
                    disabled={!isOnline}
                  >
                    {t('budget.categories.addSubcategory')}
                  </button>
                  <button
                    type="button"
                    className="button button--ghost"
                    onClick={() => setEditingCategory({ open: true, category })}
                    disabled={!isOnline}
                  >
                    {t('chores.actions.edit')}
                  </button>
                  <button
                    type="button"
                    className="button button--danger"
                    onClick={() => setPendingCategory(category)}
                    disabled={!isOnline}
                  >
                    {t('common.delete')}
                  </button>
                </div>
              </div>

              {category.subcategories.length > 0 ? (
                <ul
                  className="budget-tree__children"
                  aria-label={t('budget.categories.subcategoriesOf', { name: category.name })}
                >
                  {category.subcategories.map((subcategory) => (
                    <li className="budget-tree__child" key={subcategory.id}>
                      <span className="budget-tree__bullet" aria-hidden="true" />
                      <span className="budget-tree__child-name">{subcategory.name}</span>
                      <div className="budget-tree__child-actions">
                        <button
                          type="button"
                          className="button button--ghost"
                          onClick={() => setEditingSubcategory({ open: true, category, subcategory })}
                          disabled={!isOnline}
                        >
                          {t('chores.actions.edit')}
                        </button>
                        <button
                          type="button"
                          className="button button--danger"
                          onClick={() => setPendingSubcategory(subcategory)}
                          disabled={!isOnline}
                        >
                          {t('common.delete')}
                        </button>
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="budget-tree__empty">{t('budget.categories.noSubcategories')}</p>
              )}
            </li>
          ))}
        </ul>
      ) : null}

      {editingCategory.open ? (
        <CategoryDialog
          key={editingCategory.category?.id ?? 'new'}
          category={editingCategory.category}
          onClose={() => setEditingCategory({ open: false, category: null })}
        />
      ) : null}

      {editingSubcategory?.open ? (
        <SubcategoryDialog
          key={editingSubcategory.subcategory?.id ?? `new-${editingSubcategory.category.id}`}
          category={editingSubcategory.category}
          subcategory={editingSubcategory.subcategory}
          onClose={() => setEditingSubcategory(null)}
        />
      ) : null}

      <ConfirmDialog
        open={pendingCategory !== null}
        title={t('budget.categories.deleteCategoryTitle', { name: pendingCategory?.name ?? '' })}
        body={<p>{t('budget.categories.deleteBody')}</p>}
        confirmLabel={t('common.delete')}
        busy={removeCategory.isPending}
        onConfirm={() => {
          const category = pendingCategory
          setPendingCategory(null)
          if (category) {
            void runDelete(() => removeCategory.mutateAsync(category.id))
          }
        }}
        onCancel={() => setPendingCategory(null)}
      />

      <ConfirmDialog
        open={pendingSubcategory !== null}
        title={t('budget.categories.deleteSubcategoryTitle', { name: pendingSubcategory?.name ?? '' })}
        body={<p>{t('budget.categories.deleteBody')}</p>}
        confirmLabel={t('common.delete')}
        busy={removeSubcategory.isPending}
        onConfirm={() => {
          const subcategory = pendingSubcategory
          setPendingSubcategory(null)
          if (subcategory) {
            void runDelete(() => removeSubcategory.mutateAsync(subcategory.id))
          }
        }}
        onCancel={() => setPendingSubcategory(null)}
      />
    </>
  )
}

function CategoryDialog({ category, onClose }: { category: BudgetCategory | null; onClose: () => void }) {
  const { t } = useTranslation()
  const create = useCreateBudgetCategory()
  const update = useUpdateBudgetCategory()

  const [values, setValues] = useState<BudgetCategoryFormValues>({
    name: category?.name ?? '',
    sortOrder: String(category?.sortOrder ?? 0),
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = budgetCategoryFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    const model = { name: parsed.data.name, sortOrder: parsed.data.sortOrder, isArchived: false }

    try {
      if (category) {
        await update.mutateAsync({ categoryId: category.id, model })
      } else {
        await create.mutateAsync(model)
      }

      onClose()
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])))
      } else {
        setErrorKey(errorMessageKey(error))
      }
    }
  }

  return (
    <FormDialog
      open
      title={category ? t('budget.categories.editCategory') : t('budget.categories.addCategory')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <Field label={t('budget.field.name')} htmlFor="category-name" error={errorFor('name')}>
          <input
            id="category-name"
            className="input"
            type="text"
            maxLength={200}
            value={values.name}
            onChange={(event) => setValues((current) => ({ ...current, name: event.target.value }))}
          />
        </Field>

        <Field label={t('budget.field.sortOrder')} htmlFor="category-order" error={errorFor('sortOrder')}>
          <input
            id="category-order"
            className="input"
            type="number"
            inputMode="numeric"
            value={values.sortOrder}
            onChange={(event) => setValues((current) => ({ ...current, sortOrder: event.target.value }))}
          />
        </Field>
      </div>
    </FormDialog>
  )
}

function SubcategoryDialog({
  category,
  subcategory,
  onClose,
}: {
  category: BudgetCategory
  subcategory: BudgetSubcategory | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const create = useCreateBudgetSubcategory()
  const update = useUpdateBudgetSubcategory()

  const [values, setValues] = useState<BudgetCategoryFormValues>({
    name: subcategory?.name ?? '',
    sortOrder: String(subcategory?.sortOrder ?? 0),
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = budgetCategoryFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    const model = { name: parsed.data.name, sortOrder: parsed.data.sortOrder, isArchived: false }

    try {
      if (subcategory) {
        await update.mutateAsync({ subcategoryId: subcategory.id, model })
      } else {
        await create.mutateAsync({ categoryId: category.id, model })
      }

      onClose()
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])))
      } else {
        setErrorKey(errorMessageKey(error))
      }
    }
  }

  return (
    <FormDialog
      open
      title={subcategory ? t('budget.categories.editSubcategory') : t('budget.categories.addSubcategory')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <p className="dialog__subject">{category.name}</p>

        <Field label={t('budget.field.name')} htmlFor="subcategory-name" error={errorFor('name')}>
          <input
            id="subcategory-name"
            className="input"
            type="text"
            maxLength={200}
            value={values.name}
            onChange={(event) => setValues((current) => ({ ...current, name: event.target.value }))}
          />
        </Field>

        <Field label={t('budget.field.sortOrder')} htmlFor="subcategory-order" error={errorFor('sortOrder')}>
          <input
            id="subcategory-order"
            className="input"
            type="number"
            inputMode="numeric"
            value={values.sortOrder}
            onChange={(event) => setValues((current) => ({ ...current, sortOrder: event.target.value }))}
          />
        </Field>
      </div>
    </FormDialog>
  )
}
