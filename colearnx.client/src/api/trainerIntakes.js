import { apiRequest } from './client.js';

const intakePath = (id) => `/api/trainer/intakes/${encodeURIComponent(id)}`;

// Mutations return the whole Intake, including the new parent version.
export const trainerIntakesApi = {
  list: (token, signal) => apiRequest('/api/trainer/intakes', { token, signal }),
  get: (token, id, signal) => apiRequest(intakePath(id), { token, signal }),
  publishedCourses: (token, signal) => apiRequest('/api/courses', { token, signal }),
  create: (token, courseId, body) => apiRequest(`/api/trainer/courses/${encodeURIComponent(courseId)}/intakes`, { method: 'POST', token, body }),
  update: (token, id, body) => apiRequest(intakePath(id), { method: 'PUT', token, body }),
  createSession: (token, id, body) => apiRequest(`${intakePath(id)}/sessions`, { method: 'POST', token, body }),
  updateSession: (token, id, sessionId, body) => apiRequest(`${intakePath(id)}/sessions/${encodeURIComponent(sessionId)}`, { method: 'PUT', token, body }),
  deleteSession: (token, id, sessionId, version) => apiRequest(`${intakePath(id)}/sessions/${encodeURIComponent(sessionId)}?${new URLSearchParams({ version })}`, { method: 'DELETE', token }),
  submit: (token, id, version) => apiRequest(`${intakePath(id)}/submit`, { method: 'POST', token, body: { version } }),
  requestChange: (token, id, body) => apiRequest(`${intakePath(id)}/change-requests`, { method: 'POST', token, body }),
};
