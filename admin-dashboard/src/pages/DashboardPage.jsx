import { Bell, ClipboardList, Crown, MessageSquare, Pill, Users } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, LoadingState, PageHeader, StatCard } from '../components/ui.jsx';
import { formatDateTime, normalizeText, roleLabel } from '../utils/format.js';

export function DashboardPage() {
  const [data, setData] = useState({ users: [], medications: [], support: [], surveys: [], premium: [], scans: [] });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError('');
      try {
        const [users, medications, support, surveys, premium, scans] = await Promise.all([
          api.get(routes.users),
          api.get(routes.medications),
          api.get(routes.support),
          api.get(routes.surveys),
          api.get(routes.premiumSubscriptions),
          api.get(routes.medicineScans)
        ]);
        if (mounted) {
          setData({
            users: users.data || [],
            medications: medications.data || [],
            support: support.data || [],
            surveys: surveys.data || [],
            premium: premium.data || [],
            scans: scans.data || []
          });
        }
      } catch (err) {
        if (mounted) setError(getErrorMessage(err, 'Unable to load dashboard statistics.'));
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const stats = useMemo(() => {
    const admins = data.users.filter((user) => roleLabel(user.Role || user.role) === 'Admin').length;
    const openTickets = data.support.filter((ticket) => normalizeText(ticket.status) === 'open').length;
    const unansweredTickets = data.support.filter((ticket) => !ticket.adminReply).length;
    const requests = data.surveys.filter((survey) => survey.type === 'MedicationRequest').length;
    const premiumActive = data.premium.filter((user) => user.isActive || user.IsActive).length;
    const scansSuccessful = data.scans.filter((scan) => scan.success || scan.Success).length;
    return { admins, openTickets, unansweredTickets, requests, premiumActive, scansSuccessful };
  }, [data]);

  const recentSupport = data.support.slice(0, 5);
  const recentSurveys = data.surveys.slice(0, 5);

  if (loading) return <LoadingState label="Loading dashboard..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Operational snapshot"
        description="Live summary from the existing admin APIs. Premium metrics need new backend endpoints before they can be exact."
      />
      <ErrorBanner message={error} />

      <section className="stats-grid">
        <StatCard label="Total users" value={data.users.length} detail={`${stats.admins} admin accounts`} icon={Users} tone="blue" />
        <StatCard label="Medications" value={data.medications.length} detail="Catalog records" icon={Pill} tone="green" />
        <StatCard label="Open tickets" value={stats.openTickets} detail={`${stats.unansweredTickets} awaiting reply`} icon={MessageSquare} tone="orange" />
        <StatCard label="Surveys" value={data.surveys.length} detail={`${stats.requests} medication requests`} icon={ClipboardList} tone="pink" />
        <StatCard label="Premium" value={stats.premiumActive} detail="Active subscriptions" icon={Crown} tone="purple" />
        <StatCard label="OCR scans" value={data.scans.length} detail={`${stats.scansSuccessful} successful`} icon={Bell} tone="gray" />
      </section>

      <section className="content-grid">
        <div className="panel">
          <div className="panel-header">
            <h3>Recent support</h3>
          </div>
          {recentSupport.length === 0 ? (
            <EmptyState title="No support tickets" description="New support requests will appear here." />
          ) : (
            <div className="activity-list">
              {recentSupport.map((ticket) => (
                <article className="activity-item" key={ticket.id}>
                  <div>
                    <strong>{ticket.category}</strong>
                    <span>{ticket.userEmail || `User #${ticket.userId}`}</span>
                  </div>
                  <span className={`status-pill status-${normalizeText(ticket.status)}`}>{ticket.status}</span>
                </article>
              ))}
            </div>
          )}
        </div>

        <div className="panel">
          <div className="panel-header">
            <h3>Recent surveys</h3>
          </div>
          {recentSurveys.length === 0 ? (
            <EmptyState title="No surveys" description="Feedback and medication requests will appear here." />
          ) : (
            <div className="activity-list">
              {recentSurveys.map((survey) => (
                <article className="activity-item" key={survey.id}>
                  <div>
                    <strong>{survey.type}</strong>
                    <span>{survey.userEmail || `User #${survey.userId}`} • {formatDateTime(survey.createdAt)}</span>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}
