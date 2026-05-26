import { Edit, Plus, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, Modal, PageHeader, SearchInput } from '../components/ui.jsx';
import { formatDate, normalizeText, roleLabel } from '../utils/format.js';

const blankUser = {
  name: '',
  username: '',
  email: '',
  password: '',
  phone: '',
  birthDate: '',
  gender: '',
  roleId: 2
};

export function UsersPage() {
  const [users, setUsers] = useState([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState(blankUser);

  async function loadUsers() {
    setLoading(true);
    setError('');
    try {
      const response = await api.get(routes.users);
      setUsers(response.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load users.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadUsers();
  }, []);

  const filteredUsers = useMemo(() => {
    const needle = normalizeText(query);
    if (!needle) return users;
    return users.filter((user) =>
      [user.name, user.username, user.email, user.phone, roleLabel(user.role || user.Role)]
        .some((value) => normalizeText(value).includes(needle))
    );
  }, [users, query]);

  function openCreate() {
    setForm(blankUser);
    setModal({ type: 'create', title: 'Add user' });
  }

  function openEdit(user) {
    setForm({
      name: user.name || '',
      username: user.username || '',
      email: user.email || '',
      password: '',
      phone: user.phone || '',
      birthDate: user.birthDate ? user.birthDate.slice(0, 10) : '',
      gender: user.gender || '',
      roleId: user.role?.roleId || user.Role?.RoleId || 2,
      id: user.id
    });
    setModal({ type: 'edit', title: `Edit ${user.email}` });
  }

  async function saveUser(event) {
    event.preventDefault();
    setSaving(true);
    setError('');
    const payload = {
      name: form.name || null,
      username: form.username || null,
      phone: form.phone || null,
      birthDate: form.birthDate || null,
      gender: form.gender || null,
      roleId: Number(form.roleId)
    };

    if (form.password) payload.password = form.password;
    if (modal.type === 'create') {
      payload.email = form.email;
      payload.password = form.password;
    }

    try {
      if (modal.type === 'create') {
        await api.post('/api/Users', payload);
      } else {
        await api.put(routes.user(form.id), payload);
      }
      setModal(null);
      await loadUsers();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to save user.'));
    } finally {
      setSaving(false);
    }
  }

  async function deleteUser(user) {
    const confirmed = window.confirm(`Delete ${user.email}? This also removes related schedules, surveys, and alerts.`);
    if (!confirmed) return;
    setError('');
    try {
      await api.delete(routes.user(user.id));
      await loadUsers();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to delete user.'));
    }
  }

  if (loading) return <LoadingState label="Loading users..." />;

  return (
    <div className="stack">
      <PageHeader
        title="User management"
        description="Create, update, and remove Admin or Patient accounts through the existing Users API."
        actions={
          <button className="primary-button" type="button" onClick={openCreate}>
            <Plus size={17} />
            Add user
          </button>
        }
      />
      <ErrorBanner message={error} />

      <div className="toolbar">
        <SearchInput value={query} onChange={setQuery} placeholder="Search users by name, email, phone, or role" />
      </div>

      <div className="table-panel">
        {filteredUsers.length === 0 ? (
          <EmptyState title="No users found" description="Try a different search term or add a new account." />
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Contact</th>
                <th>Role</th>
                <th>Joined</th>
                <th className="right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map((user) => (
                <tr key={user.id}>
                  <td>
                    <strong>{user.name || user.username || 'Unnamed user'}</strong>
                    <span>#{user.id} • {user.username || 'No username'}</span>
                  </td>
                  <td>
                    <strong>{user.email}</strong>
                    <span>{user.phone || 'No phone'}</span>
                  </td>
                  <td><span className="role-pill">{roleLabel(user.role || user.Role)}</span></td>
                  <td>{formatDate(user.createdAt)}</td>
                  <td className="right action-cell">
                    <button className="icon-button" type="button" onClick={() => openEdit(user)} aria-label="Edit user" title="Edit user">
                      <Edit size={17} />
                    </button>
                    <button className="icon-button danger" type="button" onClick={() => deleteUser(user)} aria-label="Delete user" title="Delete user">
                      <Trash2 size={17} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal && (
        <Modal title={modal.title} onClose={() => setModal(null)}>
          <form className="form-grid" onSubmit={saveUser}>
            <Field label="Name">
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </Field>
            <Field label="Username">
              <input value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} />
            </Field>
            <Field label="Email">
              <input
                type="email"
                value={form.email}
                disabled={modal.type === 'edit'}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
                required={modal.type === 'create'}
              />
            </Field>
            <Field label={modal.type === 'create' ? 'Password' : 'New password'}>
              <input
                type="password"
                value={form.password}
                onChange={(event) => setForm({ ...form, password: event.target.value })}
                required={modal.type === 'create'}
              />
            </Field>
            <Field label="Phone">
              <input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
            </Field>
            <Field label="Birth date">
              <input type="date" value={form.birthDate} onChange={(event) => setForm({ ...form, birthDate: event.target.value })} />
            </Field>
            <Field label="Gender">
              <select value={form.gender} onChange={(event) => setForm({ ...form, gender: event.target.value })}>
                <option value="">Not set</option>
                <option value="Female">Female</option>
                <option value="Male">Male</option>
                <option value="Other">Other</option>
              </select>
            </Field>
            <Field label="Role">
              <select value={form.roleId} onChange={(event) => setForm({ ...form, roleId: event.target.value })}>
                <option value={2}>Patient</option>
                <option value={1}>Admin</option>
              </select>
            </Field>
            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setModal(null)}>Cancel</button>
              <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Saving...' : 'Save user'}</button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
