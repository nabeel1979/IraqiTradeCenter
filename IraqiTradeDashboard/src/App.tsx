import { Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from '@/components/layout/Layout';
import { AuthGuard } from '@/lib/auth/auth-guard';
import { LoginPage } from '@/pages/auth/LoginPage';
import { DashboardPage } from '@/pages/dashboard/DashboardPage';
import { InvoicesListPage } from '@/pages/invoices/InvoicesListPage';
import { CreateInvoicePage } from '@/pages/invoices/CreateInvoicePage';
import { ItemsListPage } from '@/pages/inventory/ItemsListPage';
import { StockMovementsPage } from '@/pages/inventory/StockMovementsPage';
import { CustomersListPage } from '@/pages/customers/CustomersListPage';
import { SalesRepsListPage } from '@/pages/sales-reps/SalesRepsListPage';
import { IncomingOrdersListPage } from '@/pages/orders/IncomingOrdersListPage';
import { AccountsTreePage } from '@/pages/accounting/AccountsTreePage';
import { JournalEntriesPage } from '@/pages/accounting/JournalEntriesPage';
import { TrialBalancePage } from '@/pages/accounting/TrialBalancePage';
import { CompaniesListPage } from '@/pages/companies/CompaniesListPage';
import { SettingsPage } from '@/pages/settings/SettingsPage';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <AuthGuard>
            <Layout />
          </AuthGuard>
        }
      >
        <Route index element={<DashboardPage />} />

        {/* Invoices */}
        <Route path="invoices" element={<InvoicesListPage />} />
        <Route path="invoices/new" element={<CreateInvoicePage />} />

        {/* Inventory */}
        <Route path="inventory" element={<ItemsListPage />} />
        <Route path="inventory/movements" element={<StockMovementsPage />} />

        {/* Customers */}
        <Route path="customers" element={<CustomersListPage />} />

        {/* Sales Reps */}
        <Route path="sales-reps" element={<SalesRepsListPage />} />

        {/* Orders */}
        <Route path="orders" element={<IncomingOrdersListPage />} />

        {/* Accounting */}
        <Route path="accounting/accounts" element={<AccountsTreePage />} />
        <Route path="accounting/journal" element={<JournalEntriesPage />} />
        <Route path="accounting/trial-balance" element={<TrialBalancePage />} />

        {/* Companies */}
        <Route path="companies" element={<CompaniesListPage />} />

        {/* Settings */}
        <Route path="settings" element={<SettingsPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
