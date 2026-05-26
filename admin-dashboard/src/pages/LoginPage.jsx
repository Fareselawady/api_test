import { LockKeyhole, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { API_BASE_URL, getErrorMessage } from '../api/client.js';
import { ErrorBanner, Field } from '../components/ui.jsx';
import { useAuth } from '../context/AuthContext.jsx';

export function LoginPage() {
  const { isAuthenticated, isAdmin, login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  if (isAuthenticated && isAdmin) {
    return <Navigate to="/" replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setError('');
    try {
      await login(form.email, form.password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to sign in.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-page">
      <section className="login-panel">
        <div className="login-copy">
          <div className="brand-mark large">M</div>
          <p className="eyebrow">Admin access</p>
          <h1>Medicine operations dashboard</h1>
          <p>Sign in with an existing Admin account to manage users, medications, support tickets, surveys, and alerts.</p>
          <div className="login-chip">
            <ShieldCheck size={16} />
            API base: {API_BASE_URL}
          </div>
        </div>

        <form className="login-card" onSubmit={handleSubmit}>
          <div className="form-title">
            <LockKeyhole size={21} />
            <h2>Admin login</h2>
          </div>
          <ErrorBanner message={error} />
          <Field label="Email">
            <input
              type="email"
              value={form.email}
              onChange={(event) => setForm({ ...form, email: event.target.value })}
              autoComplete="email"
              required
            />
          </Field>
          <Field label="Password">
            <input
              type="password"
              value={form.password}
              onChange={(event) => setForm({ ...form, password: event.target.value })}
              autoComplete="current-password"
              required
            />
          </Field>
          <button className="primary-button full-width" disabled={loading} type="submit">
            {loading ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
