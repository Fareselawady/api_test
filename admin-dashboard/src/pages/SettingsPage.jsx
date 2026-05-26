import { Server } from 'lucide-react';
import { API_BASE_URL } from '../api/client.js';
import { EndpointGap, PageHeader, StatCard } from '../components/ui.jsx';

export function SettingsPage() {
  return (
    <div className="stack">
      <PageHeader
        title="Admin settings"
        description="Runtime configuration and backend capability notes for this standalone dashboard."
      />

      <section className="stats-grid compact">
        <StatCard label="API base URL" value={API_BASE_URL} detail="Set with VITE_API_BASE_URL" icon={Server} tone="blue" />
      </section>

      <EndpointGap title="Backend security">
        `GET /api/Users/all-users` is now protected with `[Authorize(Roles = "Admin")]`, so user data is only available to authenticated admins.
      </EndpointGap>

      <EndpointGap title="Future monitoring">
        OCR scan history is now persisted and exposed. Chatbot monitoring returns a not-configured response until chatbot conversations are persisted by the backend.
      </EndpointGap>
    </div>
  );
}
