import { Reply, RotateCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, Modal, PageHeader, SearchInput } from '../components/ui.jsx';
import { formatDateTime, normalizeText } from '../utils/format.js';

const surveyTypes = ['', 'GeneralFeedback', 'Complaint', 'MedicationRequest', 'Other'];

export function SurveysPage() {
  const [surveys, setSurveys] = useState([]);
  const [query, setQuery] = useState('');
  const [type, setType] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [replyTarget, setReplyTarget] = useState(null);
  const [reply, setReply] = useState('');

  async function loadSurveys() {
    setLoading(true);
    setError('');
    try {
      const response = await api.get(routes.surveys, { params: { type: type || undefined } });
      setSurveys(response.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load survey feedback.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadSurveys();
  }, [type]);

  const filteredSurveys = useMemo(() => {
    const needle = normalizeText(query);
    if (!needle) return surveys;
    return surveys.filter((survey) =>
      [survey.userName, survey.userEmail, survey.type, survey.message]
        .some((value) => normalizeText(value).includes(needle))
    );
  }, [surveys, query]);

  async function sendReply(event) {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      await api.post(routes.surveyReply(replyTarget.userId), { message: reply });
      setReplyTarget(null);
      setReply('');
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to send survey reply.'));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState label="Loading surveys..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Survey feedback"
        description="Review feedback, complaints, and medication requests. Replies create user alerts through the backend."
        actions={
          <button className="ghost-button" type="button" onClick={loadSurveys}>
            <RotateCcw size={17} />
            Refresh
          </button>
        }
      />
      <ErrorBanner message={error} />

      <div className="toolbar split">
        <SearchInput value={query} onChange={setQuery} placeholder="Search survey messages" />
        <select value={type} onChange={(event) => setType(event.target.value)}>
          {surveyTypes.map((item) => <option key={item || 'all'} value={item}>{item || 'All survey types'}</option>)}
        </select>
      </div>

      <div className="table-panel">
        {filteredSurveys.length === 0 ? (
          <EmptyState title="No survey feedback found" description="Feedback and medication requests will appear here." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Survey</th>
                <th>User</th>
                <th>Message</th>
                <th className="right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredSurveys.map((survey) => (
                <tr key={survey.id}>
                  <td>
                    <strong>{survey.type}</strong>
                    <span>{formatDateTime(survey.createdAt)}</span>
                  </td>
                  <td>
                    <strong>{survey.userName || `User #${survey.userId}`}</strong>
                    <span>{survey.userEmail || 'No email returned'}</span>
                  </td>
                  <td className="message-cell">{survey.message}</td>
                  <td className="right">
                    <button className="ghost-button compact" type="button" onClick={() => { setReplyTarget(survey); setReply(''); }}>
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

      {replyTarget && (
        <Modal title={`Reply to ${replyTarget.userEmail || `user #${replyTarget.userId}`}`} onClose={() => setReplyTarget(null)}>
          <form className="stack" onSubmit={sendReply}>
            <div className="quoted-message">{replyTarget.message}</div>
            <Field label="Admin message">
              <textarea value={reply} onChange={(event) => setReply(event.target.value)} rows={5} required />
            </Field>
            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setReplyTarget(null)}>Cancel</button>
              <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Sending...' : 'Send reply'}</button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
