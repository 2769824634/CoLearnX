import { apiRequest } from './client';

// API modules — keep export names: authApi, coursesApi, enrollmentsApi, creditsApi
export const authApi = {
  login: (email, password, activeRole) =>
    apiRequest('/api/auth/login', {
      method: 'POST',
      body: { email, password, activeRole },
    }),
  availableRoles: (email, password) =>
    apiRequest('/api/auth/available-roles', {
      method: 'POST',
      body: { email, password },
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

export const adminAuthApi = {
  login: (email, password) =>
    apiRequest('/api/admin/auth/login', {
      method: 'POST',
      body: { email, password },
    }),
  me: (token) => apiRequest('/api/admin/auth/me', { token }),
};

export const adminAuditApi = {
  list: (token, limit = 100, signal) =>
    apiRequest(`/api/admin/audit-logs?limit=${limit}`, { token, signal }),
  query: (token, params, signal) => {
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') query.set(key, value);
    });
    return apiRequest(`/api/admin/audit-logs?${query}`, { token, signal });
  },
};

export const adminRoleRequestsApi = {
  list: (token, status, signal) => {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    return apiRequest(`/api/admin/role-requests${query}`, { token, signal });
  },
  review: (token, roleRequestId, decision, reason) =>
    apiRequest(`/api/admin/role-requests/${roleRequestId}/review`, {
      method: 'POST',
      token,
      body: { decision, reason: reason || null },
    }),
};

export const adminCourseReviewsApi = {
  list: (token, status, signal) => {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    return apiRequest(`/api/admin/courses${query}`, { token, signal });
  },
  review: (token, courseId, decision, reason) =>
    apiRequest(`/api/admin/courses/${courseId}/review`, {
      method: 'POST',
      token,
      body: { decision, reason: reason || null },
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
  addWishlist: (id) => apiRequest(`/api/courses/${id}/wishlist`, { method: 'POST' }),
  removeWishlist: (id) => apiRequest(`/api/courses/${id}/wishlist`, { method: 'DELETE' }),
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
  request: (enrollmentId) => apiRequest('/api/certificates/requests', { method: 'POST', body: { enrollmentId } }),
};

export const usersApi = {
  update: (id, payload) =>
    apiRequest(`/api/users/${id}`, { method: 'PUT', body: payload }),
};
