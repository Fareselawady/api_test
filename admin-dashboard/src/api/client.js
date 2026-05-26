import axios from 'axios';

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5135';
export const TOKEN_KEY = 'medicine_admin_jwt';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY);
    }
    return Promise.reject(error);
  }
);

export function getErrorMessage(error, fallback = 'Something went wrong.') {
  const data = error?.response?.data;
  return data?.message || data?.Message || data?.title || error?.message || fallback;
}

export function decodeJwt(token) {
  try {
    const payload = token.split('.')[1];
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
  } catch {
    return {};
  }
}

export function getRoleFromToken(token) {
  const claims = decodeJwt(token);
  return (
    claims.role ||
    claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
    ''
  );
}

export const routes = {
  login: '/api/Auth/login',
  adminCheck: '/api/Auth/admin-only',
  users: '/api/Users/all-users',
  user: (id) => `/api/Users/${id}`,
  medications: '/api/Medications/all',
  addMedication: '/api/Medications/add',
  updateMedication: (id) => `/api/Medications/${id}`,
  deleteMedication: (id) => `/api/Medications/delete/${id}`,
  ingredients: '/api/MedIngredients/all',
  ingredientsByMedication: (name) => `/api/MedIngredients/by-med/${encodeURIComponent(name)}`,
  addIngredients: '/api/MedIngredients/add',
  support: '/api/admin/support',
  supportReply: (id) => `/api/admin/support/${id}/reply`,
  supportStatus: (id) => `/api/admin/support/${id}/status`,
  surveys: '/api/Surveys',
  surveysByUser: (id) => `/api/Surveys/user/${id}`,
  surveyReply: (id) => `/api/Surveys/reply/${id}`,
  alertsForUser: (id) => `/api/users/${id}/alerts`,
  unreadCountForUser: (id) => `/api/users/${id}/alerts/unread-count`,
  readAllForUser: (id) => `/api/users/${id}/alerts/read-all`,
  cleanupAlerts: '/api/alerts/cleanup',
  premiumSubscriptions: '/api/admin/premium/subscriptions',
  activatePremiumForUser: (id) => `/api/admin/users/${id}/premium/activate`,
  cancelPremiumForUser: (id) => `/api/admin/users/${id}/premium/cancel`,
  adminAlerts: '/api/admin/alerts',
  medicineScans: '/api/admin/medicine-scans',
  chatbotHistory: '/api/admin/chatbot/history'
};
