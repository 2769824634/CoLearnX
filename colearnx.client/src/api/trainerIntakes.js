import { apiRequest } from './client.js';

const intakePath = (id) => `/api/trainer/intakes/${encodeURIComponent(id)}`;

export const trainerIntakesApi = {
  list: (token, signal) => apiRequest('/api/trainer/intakes', { token, signal }),
  get: (token, id, signal) => apiRequest(intakePath(id), { token, signal }),
  invitations: (token, signal) => apiRequest('/api/trainer/intake-invitations', { token, signal }),
  acceptInvitation: (token, id) => apiRequest(`/api/trainer/intake-invitations/${encodeURIComponent(id)}/accept`, { method: 'POST', token }),
  declineInvitation: (token, id, reason) => apiRequest(`/api/trainer/intake-invitations/${encodeURIComponent(id)}/decline`, { method: 'POST', token, body: { reason: reason || null } }),
  publishedCourses: (token, signal) => apiRequest('/api/courses', { token, signal }),
};
