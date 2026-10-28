import { apiRequest } from './client.js';

const intakePath = (id) => `/api/trainer/intakes/${encodeURIComponent(id)}`;

export const trainerIntakesApi = {
  list: (token, signal) => apiRequest('/api/trainer/intakes', { token, signal }),
  get: (token, id, signal) => apiRequest(intakePath(id), { token, signal }),
  publishedCourses: (token, signal) => apiRequest('/api/courses', { token, signal }),
};
