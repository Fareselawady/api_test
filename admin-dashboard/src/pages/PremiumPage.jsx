import { Crown, RefreshCw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, PageHeader, SearchInput, StatCard } from '../components/ui.jsx';
import { formatDate, normalizeText } from '../utils/format.js';

const plans = ['Month', 'ThreeMonths', 'Year'];

export function PremiumPage() {
  const [subscriptions, setSubscriptions] = useState([]);
  const [query, setQuery] = useState('');
  const [planByUser, setPlanByUser] = useState({});
  const [busyUserId, setBusyUserId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  async function loadSubscriptions() {
    setLoading(true);
    setError('');
    try {
      const response = await api.get(routes.premiumSubscriptions);
      setSubscriptions(response.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load premium subscriptions.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadSubscriptions();
  }, []);

  const filteredSubscriptions = useMemo(() => {
    const needle = normalizeText(query);
    if (!needle) return subscriptions;
    return subscriptions.filter((user) => [user.name, user.email, user.username, user.phone]
      .some((value) => normalizeText(value).includes(needle)));
  }, [subscriptions, query]);

  const activeCount = subscriptions.filter((user) => user.isActive || user.IsActive).length;
  const expiredCount = subscriptions.filter((user) => (user.isPremium || user.IsPremium) && !(user.isActive || user.IsActive)).length;

  async function activate(userId) {
    setBusyUserId(userId);
    setError('');
    try {
      await api.post(routes.activatePremiumForUser(userId), { plan: planByUser[userId] || 'Month' });
      await loadSubscriptions();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to activate premium.'));
    } finally {
      setBusyUserId(null);
    }
  }

  async function cancel(userId) {
    const confirmed = window.confirm('Cancel premium for this user?');
    if (!confirmed) return;
    setBusyUserId(userId);
    setError('');
    try {
      await api.post(routes.cancelPremiumForUser(userId));
      await loadSubscriptions();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to cancel premium.'));
    } finally {
      setBusyUserId(null);
    }
  }

  if (loading) return <LoadingState label="Loading premium subscriptions..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Premium subscriptions"
        description="View all users, activate subscriptions, extend active plans, and cancel premium access."
        actions={
          <button className="ghost-button" type="button" onClick={loadSubscriptions}>
            <RefreshCw size={17} />
            Refresh
          </button>
        }
      />
      <ErrorBanner message={error} />

      <section className="stats-grid compact">
        <StatCard label="Active premium" value={activeCount} detail="Users with unexpired premium" icon={Crown} tone="purple" />
        <StatCard label="Expired premium flags" value={expiredCount} detail="Marked premium but not active" icon={Crown} tone="orange" />
      </section>

      <div className="toolbar">
        <SearchInput value={query} onChange={setQuery} placeholder="Search users by name, email, username, or phone" />
      </div>

      <div className="table-panel">
        {filteredSubscriptions.length === 0 ? (
          <EmptyState title="No users found" description="Try a different search term." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Status</th>
                <th>Start</th>
                <th>End</th>
                <th>Remaining</th>
                <th className="right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredSubscriptions.map((user) => {
                const userId = user.id || user.Id;
                const isActive = user.isActive || user.IsActive;
                const isPremium = user.isPremium || user.IsPremium;
                const remainingDays = user.remainingDays ?? user.RemainingDays;
                return (
                  <tr key={userId}>
                    <td>
                      <strong>{user.name || user.Name || user.email || user.Email}</strong>
                      <span>{user.email || user.Email}</span>
                    </td>
                    <td>
                      <span className={`status-pill ${isActive ? 'status-closed' : isPremium ? 'status-inprogress' : 'status-open'}`}>
                        {isActive ? 'Active' : isPremium ? 'Expired' : 'Free'}
                      </span>
                    </td>
                    <td>{formatDate(user.premiumStartDate || user.PremiumStartDate)}</td>
                    <td>{formatDate(user.premiumEndDate || user.PremiumEndDate)}</td>
                    <td>{remainingDays == null ? 'Not set' : `${remainingDays} days`}</td>
                    <td className="right action-cell">
                      <Field label="Plan">
                        <select
                          value={planByUser[userId] || 'Month'}
                          onChange={(event) => setPlanByUser({ ...planByUser, [userId]: event.target.value })}
                        >
                          {plans.map((plan) => <option key={plan} value={plan}>{plan}</option>)}
                        </select>
                      </Field>
                      <button className="primary-button" type="button" disabled={busyUserId === userId} onClick={() => activate(userId)}>
                        {isActive ? 'Extend' : 'Activate'}
                      </button>
                      <button className="ghost-button danger-text" type="button" disabled={busyUserId === userId || !isPremium} onClick={() => cancel(userId)}>
                        Cancel
                      </button>
                    </td>
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
