import {
  Bell,
  Bot,
  ClipboardList,
  Crown,
  Gauge,
  LogOut,
  Menu,
  MessageSquare,
  Pill,
  Settings,
  Users,
  X
} from 'lucide-react';
import { useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext.jsx';

const navigation = [
  { to: '/', label: 'Home', icon: Gauge, end: true },
  { to: '/users', label: 'Users', icon: Users },
  { to: '/medications', label: 'Medications', icon: Pill },
  { to: '/premium', label: 'Premium', icon: Crown },
  { to: '/support', label: 'Support', icon: MessageSquare },
  { to: '/surveys', label: 'Surveys', icon: ClipboardList },
  { to: '/alerts', label: 'Alerts', icon: Bell },
  { to: '/monitoring', label: 'Monitoring', icon: Bot },
  { to: '/settings', label: 'Settings', icon: Settings }
];

const titles = {
  '/': 'Admin overview',
  '/users': 'Users',
  '/medications': 'Medications',
  '/premium': 'Premium subscriptions',
  '/support': 'Support messages',
  '/surveys': 'Survey feedback',
  '/alerts': 'Alerts monitoring',
  '/monitoring': 'OCR and chatbot monitoring',
  '/settings': 'Admin settings'
};

export function Layout() {
  const { logout } = useAuth();
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const title = titles[location.pathname] || 'Admin dashboard';

  return (
    <div className="app-shell">
      <aside className={`sidebar ${open ? 'sidebar-open' : ''}`}>
        <div className="brand">
          <div className="brand-mark">M</div>
          <div>
            <strong>Medicine Admin</strong>
            <span>Operations center</span>
          </div>
        </div>

        <nav className="nav-list" aria-label="Admin navigation">
          {navigation.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
                onClick={() => setOpen(false)}
              >
                <Icon size={18} />
                <span>{item.label}</span>
              </NavLink>
            );
          })}
        </nav>

        <button className="ghost-button sidebar-logout" type="button" onClick={logout}>
          <LogOut size={18} />
          Sign out
        </button>
      </aside>

      {open && <button className="mobile-scrim" aria-label="Close navigation" onClick={() => setOpen(false)} />}

      <main className="main-panel">
        <header className="topbar">
          <button className="icon-button mobile-only" type="button" onClick={() => setOpen(true)} aria-label="Open navigation">
            <Menu size={20} />
          </button>
          <div>
            <p className="eyebrow">Dashboard</p>
            <h1>{title}</h1>
          </div>
          <button className="icon-button mobile-close" type="button" onClick={() => setOpen(false)} aria-label="Close navigation">
            <X size={20} />
          </button>
        </header>
        <div className="page-content">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
