import { Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, PageHeader, SearchInput } from '../components/ui.jsx';
import { formatDateTime, normalizeText } from '../utils/format.js';

export function AlertsPage() {
  const [users, setUsers] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [filters, setFilters] = useState({ query: '', userId: '', type: '', isRead: '' });
  const [daysOld, setDaysOld] = useState(30);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  async function loadData() {
    setLoading(true);
    setError('');
    try {
      const [userResponse, alertResponse] = await Promise.all([
        api.get(routes.users),
        api.get(routes.adminAlerts, {
          params: {
            userId: filters.userId || undefined,
            type: filters.type || undefined,
            isRead: filters.isRead === '' ? undefined : filters.isRead
          }
        })
      ]);
      setUsers(userResponse.data || []);
      setAlerts(alertResponse.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load global alerts.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, [filters.userId, filters.type, filters.isRead]);

  const alertTypes = useMemo(() => {
    return Array.from(new Set(alerts.map((alert) => alert.type || alert.Type).filter(Boolean))).sort();
  }, [alerts]);

  const filteredAlerts = useMemo(() => {
    const needle = normalizeText(filters.query);
    if (!needle) return alerts;
    return alerts.filter((alert) =>
      [
        alert.userName,
        alert.UserName,
        alert.userEmail,
        alert.UserEmail,
        alert.type,
        alert.Type,
        alert.title,
        alert.Title,
        alert.message,
        alert.Message,
        alert.medicationName,
        alert.MedicationName
      ].some((value) => normalizeText(value).includes(needle))
    );
  }, [alerts, filters.query]);

  const unreadCount = alerts.filter((alert) => !(alert.isRead ?? alert.IsRead)).length;

  async function cleanupAlerts() {
    setError('');
    try {
      await api.delete(routes.cleanupAlerts, { params: { daysOld } });
      await loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to clean up alerts.'));
    }
  }

  if (loading) return <LoadingState label="Loading global alerts..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Alerts monitoring"
        description="Monitor alerts across all users with filters for user, type, and read status."
        actions={
          <button className="ghost-button danger-text" type="button" onClick={cleanupAlerts}>
            <Trash2 size={17} />
            Cleanup read alerts
          </button>
        }
      />
      <ErrorBanner message={error} />

      <div className="toolbar split">
        <SearchInput value={filters.query} onChange={(query) => setFilters({ ...filters, query })} placeholder="Search alerts, users, medication, or messages" />
        <Field label="User">
          <select value={filters.userId} onChange={(event) => setFilters({ ...filters, userId: event.target.value })}>
            <option value="">All users</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>{user.email || `User #${user.id}`}</option>
            ))}
          </select>
        </Field>
        <Field label="Type">
          <select value={filters.type} onChange={(event) => setFilters({ ...filters, type: event.target.value })}>
            <option value="">All types</option>
            {alertTypes.map((type) => <option key={type} value={type}>{type}</option>)}
          </select>
        </Field>
        <Field label="Read status">
          <select value={filters.isRead} onChange={(event) => setFilters({ ...filters, isRead: event.target.value })}>
            <option value="">All</option>
            <option value="false">Unread</option>
            <option value="true">Read</option>
          </select>
        </Field>
        <Field label="Cleanup days">
          <input type="number" min="1" value={daysOld} onChange={(event) => setDaysOld(event.target.value)} />
        </Field>
      </div>

      <div className="summary-strip">
        <strong>{unreadCount}</strong>
        <span>unread alerts in the current result set</span>
      </div>

      <div className="table-panel">
        {filteredAlerts.length === 0 ? (
          <EmptyState title="No alerts found" description="Adjust filters or wait for new alerts." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Alert</th>
                <th>User</th>
                <th>Message</th>
                <th>Status</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {filteredAlerts.map((alert) => {
                const isRead = alert.isRead ?? alert.IsRead;
                return (
                  <tr key={alert.id || alert.Id}>
                    <td>
                      <strong>{alert.title || alert.Title || alert.type || alert.Type}</strong>
                      <span>{alert.type || alert.Type} {alert.medicationName || alert.MedicationName ? `• ${alert.medicationName || alert.MedicationName}` : ''}</span>
                    </td>
                    <td>
                      <strong>{alert.userName || alert.UserName || `User #${alert.userId || alert.UserId}`}</strong>
                      <span>{alert.userEmail || alert.UserEmail || 'No email'}</span>
                    </td>
                    <td className="message-cell">{alert.message || alert.Message}</td>
                    <td><span className={`status-pill ${isRead ? 'status-closed' : 'status-open'}`}>{isRead ? 'Read' : 'Unread'}</span></td>
                    <td>{formatDateTime(alert.createdAt || alert.CreatedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
