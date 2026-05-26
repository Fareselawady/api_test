import { Bot, ScanText } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, PageHeader, SearchInput, StatCard } from '../components/ui.jsx';
import { formatDateTime, normalizeText } from '../utils/format.js';

export function MonitoringPage() {
  const [scans, setScans] = useState([]);
  const [chatbot, setChatbot] = useState({ configured: false, items: [] });
  const [filters, setFilters] = useState({ query: '', success: '' });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  async function loadMonitoring() {
    setLoading(true);
    setError('');
    try {
      const [scanResponse, chatbotResponse] = await Promise.all([
        api.get(routes.medicineScans, {
          params: { success: filters.success === '' ? undefined : filters.success }
        }),
        api.get(routes.chatbotHistory)
      ]);
      setScans(scanResponse.data || []);
      setChatbot(chatbotResponse.data || { configured: false, items: [] });
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load monitoring data.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadMonitoring();
  }, [filters.success]);

  const filteredScans = useMemo(() => {
    const needle = normalizeText(filters.query);
    if (!needle) return scans;
    return scans.filter((scan) =>
      [
        scan.userName,
        scan.UserName,
        scan.userEmail,
        scan.UserEmail,
        scan.medicationName,
        scan.MedicationName,
        scan.message,
        scan.Message,
        scan.fileName,
        scan.FileName
      ].some((value) => normalizeText(value).includes(needle))
    );
  }, [scans, filters.query]);

  const successfulScans = scans.filter((scan) => scan.success ?? scan.Success).length;

  if (loading) return <LoadingState label="Loading monitoring data..." />;

  return (
    <div className="stack">
      <PageHeader
        title="OCR and chatbot monitoring"
        description="Review stored medicine scan activity. Chatbot monitoring will populate when chatbot persistence is added to the backend."
      />
      <ErrorBanner message={error} />

      <section className="stats-grid compact">
        <StatCard label="OCR scans" value={scans.length} detail={`${successfulScans} successful detections`} icon={ScanText} tone="green" />
        <StatCard label="Chatbot history" value={chatbot.configured ? chatbot.items?.length || 0 : 'Not configured'} detail={chatbot.message || 'No persisted chatbot data'} icon={Bot} tone="gray" />
      </section>

      <div className="toolbar split">
        <SearchInput value={filters.query} onChange={(query) => setFilters({ ...filters, query })} placeholder="Search scans by user, file, medication, or message" />
        <Field label="Scan result">
          <select value={filters.success} onChange={(event) => setFilters({ ...filters, success: event.target.value })}>
            <option value="">All results</option>
            <option value="true">Successful</option>
            <option value="false">Failed</option>
          </select>
        </Field>
      </div>

      <div className="table-panel">
        {filteredScans.length === 0 ? (
          <EmptyState title="No scan history found" description="Medicine image scans will appear here after users scan images." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Scan</th>
                <th>User</th>
                <th>Detected medication</th>
                <th>Result</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {filteredScans.map((scan) => {
                const success = scan.success ?? scan.Success;
                return (
                  <tr key={scan.id || scan.Id}>
                    <td>
                      <strong>{scan.fileName || scan.FileName || 'Uploaded image'}</strong>
                      <span>{scan.contentType || scan.ContentType || 'Unknown type'} • {scan.fileSize || scan.FileSize || 0} bytes</span>
                    </td>
                    <td>
                      <strong>{scan.userName || scan.UserName || `User #${scan.userId || scan.UserId}`}</strong>
                      <span>{scan.userEmail || scan.UserEmail || 'No email'}</span>
                    </td>
                    <td>{scan.medicationName || scan.MedicationName || 'Not detected'}</td>
                    <td>
                      <span className={`status-pill ${success ? 'status-closed' : 'status-open'}`}>
                        {success ? 'Success' : `Failed ${scan.httpStatusCode || scan.HttpStatusCode || ''}`}
                      </span>
                      <span>{scan.message || scan.Message}</span>
                    </td>
                    <td>{formatDateTime(scan.createdAt || scan.CreatedAt)}</td>
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
