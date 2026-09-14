import { apiRequest } from './client.js';

const intakePath = (id) => `/api/trainer/intakes/${encodeURIComponent(id)}`;

export const trainerLaterPhaseApi = {
  availableMaterials: (token, signal, courseId) => apiRequest(
    `/api/trainer/learning-materials${courseId ? `?courseId=${encodeURIComponent(courseId)}` : ''}`,
    { token, signal },
  ),
  intakeMaterials: (token, intakeId, signal) => apiRequest(`${intakePath(intakeId)}/learning-materials`, { token, signal }),
  attachMaterial: (token, intakeId, materialVersionId) => apiRequest(`${intakePath(intakeId)}/learning-materials`, {
    method: 'POST', token, body: { materialVersionId },
  }),
  recordings: (token, intakeId, sessionId, signal) => apiRequest(`${intakePath(intakeId)}/sessions/${encodeURIComponent(sessionId)}/recordings`, { token, signal }),
  addRecording: (token, intakeId, sessionId, body) => apiRequest(`${intakePath(intakeId)}/sessions/${encodeURIComponent(sessionId)}/recordings`, {
    method: 'POST', token, body,
  }),
  learners: (token, intakeId, signal) => apiRequest(`${intakePath(intakeId)}/learners`, { token, signal }),
  saveAttendance: (token, intakeId, sessionId, records) => apiRequest(`${intakePath(intakeId)}/sessions/${encodeURIComponent(sessionId)}/attendance`, {
    method: 'PUT', token, body: { records },
  }),
  assessments: (token, intakeId, signal) => apiRequest(`${intakePath(intakeId)}/assessments`, { token, signal }),
  createAssessment: (token, intakeId, body) => apiRequest(`${intakePath(intakeId)}/assessments`, { method: 'POST', token, body }),
  grade: (token, assessmentId, enrollmentId, body) => apiRequest(`/api/trainer/assessments/${encodeURIComponent(assessmentId)}/grades/${encodeURIComponent(enrollmentId)}`, {
    method: 'PUT', token, body,
  }),
  certificateRequests: (token, status, signal) => apiRequest(`/api/trainer/certificate-requests${status ? `?status=${encodeURIComponent(status)}` : ''}`, { token, signal }),
  reviewCertificate: (token, requestId, decision, reason) => apiRequest(`/api/trainer/certificate-requests/${encodeURIComponent(requestId)}/review`, {
    method: 'POST', token, body: { decision, reason: reason || null },
  }),
};
