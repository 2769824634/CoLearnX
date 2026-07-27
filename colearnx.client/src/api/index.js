import { apiRequest } from './client';

// API modules — keep export names: authApi, coursesApi, enrollmentsApi, creditsApi
export const authApi = {
  login: (email, password, activeRole) =>
    apiRequest('/api/auth/login', {
      method: 'POST',
      body: { email, password, activeRole },
    }),
  register: (payload) =>
    apiRequest('/api/auth/register', { method: 'POST', body: payload }),
  me: () => apiRequest('/api/auth/me'),
  switchRole: (activeRole) =>
    apiRequest('/api/auth/switch-role', {
      method: 'POST',
      body: { activeRole },
    }),
};

export const coursesApi = {
  list: (params = {}) => {
    const q = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => {
      if (v != null && v !== '') q.set(k, v);
    });
    const qs = q.toString();
    return apiRequest(`/api/courses${qs ? `?${qs}` : ''}`);
  },
  featured: () => apiRequest('/api/courses/featured'),
  get: (id) => apiRequest(`/api/courses/${id}`),
};

export const enrollmentsApi = {
  my: () => apiRequest('/api/enrollments/my'),
  enrol: (courseId, courseSessionId) =>
    apiRequest('/api/enrollments', {
      method: 'POST',
      body: { courseId, courseSessionId },
    }),
};

export const creditsApi = {
  packages: () => apiRequest('/api/credits/packages'),
  myLedger: () => apiRequest('/api/credits/ledger/my'),
  paypalConfig: () => apiRequest('/api/credits/paypal/config'),
  createPayPalOrder: (creditPackageId) =>
    apiRequest('/api/credits/paypal/create-order', {
      method: 'POST',
      body: { creditPackageId },
    }),
  capturePayPalOrder: (orderId) =>
    apiRequest('/api/credits/paypal/capture', {
      method: 'POST',
      body: { orderId },
    }),
  topUp: (creditPackageId) =>
    apiRequest('/api/credits/topup', {
      method: 'POST',
      body: { creditPackageId },
    }),
};

export const certificatesApi = {
  my: () => apiRequest('/api/certificates/my'),
};

export const usersApi = {
  update: (id, payload) =>
    apiRequest(`/api/users/${id}`, { method: 'PUT', body: payload }),
};
