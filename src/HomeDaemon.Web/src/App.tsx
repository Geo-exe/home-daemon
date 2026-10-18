import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { RequireHousehold } from './auth/RequireHousehold'
import { RequireSession } from './auth/RequireSession'
import { AppShell } from './components/AppShell'
import { Spinner } from './components/Spinner'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'

// The sign-in flow is needed immediately; every other screen is split out and fetched when it
// is first opened, which keeps the initial download small.
const ChoresListPage = lazy(() => import('./pages/ChoresListPage').then((module) => ({ default: module.ChoresListPage })))
const ChoreDetailPage = lazy(() => import('./pages/ChoreDetailPage').then((module) => ({ default: module.ChoreDetailPage })))
const ChoreFormPage = lazy(() => import('./pages/ChoreFormPage').then((module) => ({ default: module.ChoreFormPage })))
const PlannerPage = lazy(() => import('./pages/PlannerPage').then((module) => ({ default: module.PlannerPage })))
const HistoryPage = lazy(() => import('./pages/HistoryPage').then((module) => ({ default: module.HistoryPage })))
const BudgetSummaryPage = lazy(() =>
  import('./pages/BudgetSummaryPage').then((module) => ({ default: module.BudgetSummaryPage })),
)
const BudgetExpensesPage = lazy(() =>
  import('./pages/BudgetExpensesPage').then((module) => ({ default: module.BudgetExpensesPage })),
)
const BudgetExpenseFormPage = lazy(() =>
  import('./pages/BudgetExpenseFormPage').then((module) => ({ default: module.BudgetExpenseFormPage })),
)
const BudgetIncomesPage = lazy(() =>
  import('./pages/BudgetIncomesPage').then((module) => ({ default: module.BudgetIncomesPage })),
)
const BudgetTransfersPage = lazy(() =>
  import('./pages/BudgetTransfersPage').then((module) => ({ default: module.BudgetTransfersPage })),
)
const SettingsLayout = lazy(() => import('./pages/settings/SettingsLayout').then((module) => ({ default: module.SettingsLayout })))
const HouseholdSettingsPage = lazy(() =>
  import('./pages/settings/HouseholdSettingsPage').then((module) => ({ default: module.HouseholdSettingsPage })),
)
const RoomsSettingsPage = lazy(() =>
  import('./pages/settings/RoomsSettingsPage').then((module) => ({ default: module.RoomsSettingsPage })),
)
const MembersSettingsPage = lazy(() =>
  import('./pages/settings/MembersSettingsPage').then((module) => ({ default: module.MembersSettingsPage })),
)
const AccountsSettingsPage = lazy(() =>
  import('./pages/settings/AccountsSettingsPage').then((module) => ({ default: module.AccountsSettingsPage })),
)
const BudgetCategoriesSettingsPage = lazy(() =>
  import('./pages/settings/BudgetCategoriesSettingsPage').then((module) => ({
    default: module.BudgetCategoriesSettingsPage,
  })),
)

function RouteFallback() {
  const { t } = useTranslation()
  return <Spinner label={t('app.loading')} />
}

export function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        <Route element={<RequireSession />}>
          {/* Reached before the household exists, so it stays outside the household gate. */}
          <Route path="/change-password" element={<ChangePasswordPage />} />

          <Route element={<RequireHousehold />}>
            <Route element={<AppShell />}>
              <Route path="/" element={<Navigate to="/chores" replace />} />
              <Route path="/chores" element={<ChoresListPage />} />
              <Route path="/chores/new" element={<ChoreFormPage />} />
              <Route path="/chores/:choreId" element={<ChoreDetailPage />} />
              <Route path="/chores/:choreId/edit" element={<ChoreFormPage />} />
              <Route path="/planner" element={<PlannerPage />} />
              <Route path="/history" element={<HistoryPage />} />
              <Route path="/budget" element={<BudgetSummaryPage />} />
              <Route path="/budget/expenses" element={<BudgetExpensesPage />} />
              <Route path="/budget/expenses/new" element={<BudgetExpenseFormPage />} />
              <Route path="/budget/expenses/:expenseId" element={<BudgetExpenseFormPage />} />
              <Route path="/budget/incomes" element={<BudgetIncomesPage />} />
              <Route path="/budget/transfers" element={<BudgetTransfersPage />} />
              <Route path="/settings" element={<SettingsLayout />}>
                <Route index element={<Navigate to="/settings/household" replace />} />
                <Route path="household" element={<HouseholdSettingsPage />} />
                <Route path="rooms" element={<RoomsSettingsPage />} />
                <Route path="members" element={<MembersSettingsPage />} />
                <Route path="budget/categories" element={<BudgetCategoriesSettingsPage />} />
                <Route path="accounts" element={<AccountsSettingsPage />} />
              </Route>
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Route>
        </Route>
      </Routes>
    </Suspense>
  )
}
