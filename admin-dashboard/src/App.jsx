import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout.jsx';
import { ProtectedRoute } from './components/ProtectedRoute.jsx';
import { AlertsPage } from './pages/AlertsPage.jsx';
import { DashboardPage } from './pages/DashboardPage.jsx';
import { LoginPage } from './pages/LoginPage.jsx';
import { MedicationsPage } from './pages/MedicationsPage.jsx';
import { MonitoringPage } from './pages/MonitoringPage.jsx';
import { PremiumPage } from './pages/PremiumPage.jsx';
import { SettingsPage } from './pages/SettingsPage.jsx';
import { SupportPage } from './pages/SupportPage.jsx';
import { SurveysPage } from './pages/SurveysPage.jsx';
import { UsersPage } from './pages/UsersPage.jsx';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="users" element={<UsersPage />} />
        <Route path="medications" element={<MedicationsPage />} />
        <Route path="premium" element={<PremiumPage />} />
        <Route path="support" element={<SupportPage />} />
        <Route path="surveys" element={<SurveysPage />} />
        <Route path="alerts" element={<AlertsPage />} />
        <Route path="monitoring" element={<MonitoringPage />} />
        <Route path="settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
