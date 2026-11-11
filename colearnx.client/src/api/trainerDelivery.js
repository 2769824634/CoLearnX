import { apiRequest } from './client.js';

export const trainerDeliveryApi = {
  updateMeetingLink: (token, intakeId, sessionId, meetingLink, version) => apiRequest(
    `/api/trainer/intakes/${encodeURIComponent(intakeId)}/sessions/${encodeURIComponent(sessionId)}/delivery`,
    { method: 'PATCH', token, body: { meetingLink, version } },
  ),
};
