import { apiRequest } from './client.js';

const intakePath = (id) => `/api/creator/intakes/${encodeURIComponent(id)}`;

export const creatorIntakesApi = {
  list: (token, courseId, signal) => apiRequest(courseId ? `/api/creator/intakes?courseId=${encodeURIComponent(courseId)}` : '/api/creator/intakes', { token, signal }),
  get: (token, id, signal) => apiRequest(intakePath(id), { token, signal }),
  create: (token, courseId, body) => apiRequest(`/api/creator/courses/${encodeURIComponent(courseId)}/intakes`, { method: 'POST', token, body }),
  update: (token, id, body) => apiRequest(intakePath(id), { method: 'PUT', token, body }),
  createSession: (token, id, body) => apiRequest(`${intakePath(id)}/sessions`, { method: 'POST', token, body }),
  updateSession: (token, id, sessionId, body) => apiRequest(`${intakePath(id)}/sessions/${encodeURIComponent(sessionId)}`, { method: 'PUT', token, body }),
  deleteSession: (token, id, sessionId, version) => apiRequest(`${intakePath(id)}/sessions/${encodeURIComponent(sessionId)}?${new URLSearchParams({ version })}`, { method: 'DELETE', token }),
  publish: (token, id, version) => apiRequest(`${intakePath(id)}/publish`, { method: 'POST', token, body: { version } }),
  cancel: (token, id) => apiRequest(`${intakePath(id)}/cancel`, { method: 'POST', token }),
  postpone: (token, id, body) => apiRequest(`${intakePath(id)}/postpone`, { method: 'POST', token, body }),
};
