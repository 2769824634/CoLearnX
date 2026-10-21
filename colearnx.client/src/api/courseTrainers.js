import { apiRequest } from './client.js';

const rosterPath = (courseId) => `/api/creator/courses/${encodeURIComponent(courseId)}/trainers`;
const invitationPath = (id) => `/api/trainer/course-invitations/${encodeURIComponent(id)}`;

export const courseTrainersApi = {
  list: (token, courseId, signal) => apiRequest(rosterPath(courseId), { token, signal }),
  searchTrainers: (token, search, signal) => apiRequest(`/api/creator/trainers?search=${encodeURIComponent(search)}`, { token, signal }),
  invite: (token, courseId, trainerId) => apiRequest(rosterPath(courseId), { method: 'POST', token, body: { trainerId } }),
  remove: (token, courseId, trainerId) => apiRequest(`${rosterPath(courseId)}/${encodeURIComponent(trainerId)}/remove`, { method: 'POST', token }),
  myInvitations: (token, signal) => apiRequest('/api/trainer/course-invitations', { token, signal }),
  accept: (token, id) => apiRequest(`${invitationPath(id)}/accept`, { method: 'POST', token }),
  decline: (token, id) => apiRequest(`${invitationPath(id)}/decline`, { method: 'POST', token }),
};
