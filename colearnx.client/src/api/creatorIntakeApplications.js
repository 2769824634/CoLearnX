import { apiRequest } from './client.js';

const applicationPath = (id) => `/api/creator/intake-applications/${encodeURIComponent(id)}`;

export const creatorIntakeApplicationsApi = {
  list: (token, signal) => apiRequest('/api/creator/intake-applications', { token, signal }),
  get: (token, id, signal) => apiRequest(applicationPath(id), { token, signal }),
  review: (token, id, body) => apiRequest(`${applicationPath(id)}/review`, { method: 'POST', token, body }),
};
