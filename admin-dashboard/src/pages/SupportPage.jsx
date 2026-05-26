import { Reply, RotateCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, Modal, PageHeader, SearchInput } from '../components/ui.jsx';
import { formatDateTime, normalizeText } from '../utils/format.js';

const categories = ['', 'BugReport', 'TechnicalIssue', 'PaymentProblem', 'PremiumSubscription', 'Suggestion', 'Other'];
const statuses = ['', 'Open', 'InProgress', 'Closed'];

export function SupportPage() {
  const [tickets, setTickets] = useState([]);
  const [filters, setFilters] = useState({ query: '', category: '', status: '' });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [replyTicket, setReplyTicket] = useState(null);
  const [reply, setReply] = useState('');

  async function loadTickets() {
    setLoading(true);
    setError('');
    try {
      const response = await api.get(routes.support, {
        params: {
          category: filters.category || undefined,
          status: filters.status || undefined
        }
      });
      setTickets(response.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load support tickets.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadTickets();
  }, [filters.category, filters.status]);

  const filteredTickets = useMemo(() => {
    const needle = normalizeText(filters.query);
    if (!needle) return tickets;
    return tickets.filter((ticket) =>
      [ticket.userName, ticket.userEmail, ticket.category, ticket.status, ticket.message, ticket.adminReply]
        .some((value) => normalizeText(value).includes(needle))
    );
  }, [tickets, filters.query]);

  async function updateStatus(ticket, status) {
    setError('');
    try {
      await api.patch(routes.supportStatus(ticket.id), { status });
      await loadTickets();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to update ticket status.'));
    }
  }

  async function sendReply(event) {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      await api.post(routes.supportReply(replyTicket.id), { reply });
      setReplyTicket(null);
      setReply('');
      await loadTickets();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to send reply.'));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState label="Loading support tickets..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Support messages"
        description="Filter tickets, update status, and send admin replies through the support API."
        actions={
          <button className="ghost-button" type="button" onClick={loadTickets}>
            <RotateCcw size={17} />
            Refresh
          </button>
        }
      />
      <ErrorBanner message={error} />

      <div className="toolbar split">
        <SearchInput value={filters.query} onChange={(query) => setFilters({ ...filters, query })} placeholder="Search tickets" />
        <select value={filters.category} onChange={(event) => setFilters({ ...filters, category: event.target.value })}>
          {categories.map((category) => <option key={category || 'all'} value={category}>{category || 'All categories'}</option>)}
        </select>
        <select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}>
          {statuses.map((status) => <option key={status || 'all'} value={status}>{status || 'All statuses'}</option>)}
        </select>
      </div>

      <div className="table-panel">
        {filteredTickets.length === 0 ? (
          <EmptyState title="No support tickets found" description="Adjust filters or wait for new tickets." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Ticket</th>
                <th>User</th>
                <th>Message</th>
                <th>Status</th>
                <th className="right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredTickets.map((ticket) => (
                <tr key={ticket.id}>
                  <td>
                    <strong>#{ticket.id} • {ticket.category}</strong>
                    <span>{formatDateTime(ticket.createdAt)}</span>
                  </td>
                  <td>
                    <strong>{ticket.userName || `User #${ticket.userId}`}</strong>
                    <span>{ticket.userEmail || 'No email returned'}</span>
                  </td>
                  <td className="message-cell">
                    <strong>{ticket.message}</strong>
                    {ticket.adminReply && <span>Reply: {ticket.adminReply}</span>}
                  </td>
                  <td>
                    <select value={ticket.status} onChange={(event) => updateStatus(ticket, event.target.value)} aria-label="Ticket status">
                      {statuses.filter(Boolean).map((status) => <option key={status} value={status}>{status}</option>)}
                    </select>
                  </td>
                  <td className="right">
                    <button className="ghost-button compact" type="button" onClick={() => { setReplyTicket(ticket); setReply(ticket.adminReply || ''); }}>
                      <Reply size={16} />
                      Reply
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {replyTicket && (
        <Modal title={`Reply to ticket #${replyTicket.id}`} onClose={() => setReplyTicket(null)}>
          <form className="stack" onSubmit={sendReply}>
            <div className="quoted-message">{replyTicket.message}</div>
            <Field label="Admin reply">
              <textarea value={reply} onChange={(event) => setReply(event.target.value)} rows={5} required />
            </Field>
            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setReplyTicket(null)}>Cancel</button>
              <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Sending...' : 'Send reply'}</button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
