import { apiRequest } from './client.js';

export const adminLaterPhaseApi = {
  ledger: (token, filters = {}, signal) => {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => { if (value) query.set(key, value); });
    return apiRequest(`/api/admin/credits/ledger${query.size ? `?${query}` : ''}`, { token, signal });
  },
  adjustCredits: (token, body) => apiRequest('/api/admin/credits/adjustments', { method: 'POST', token, body }),
  disputes: (token, status, signal) => apiRequest(`/api/admin/disputes${status ? `?status=${encodeURIComponent(status)}` : ''}`, { token, signal }),
  reviewDispute: (token, disputeId, body) => apiRequest(`/api/admin/disputes/${encodeURIComponent(disputeId)}/review`, {
    method: 'POST', token, body,
  }),
  materialVersions: (token, status, signal) => apiRequest(`/api/admin/material-versions${status ? `?status=${encodeURIComponent(status)}` : ''}`, { token, signal }),
  reviewMaterial: (token, versionId, decision, reason) => apiRequest(`/api/admin/material-versions/${encodeURIComponent(versionId)}/review`, {
    method: 'POST', token, body: { decision, reason: reason || null },
  }),
  certificateRequests: (token, status, signal) => apiRequest(`/api/admin/certificate-requests${status ? `?status=${encodeURIComponent(status)}` : ''}`, { token, signal }),
  reviewCertificate: (token, requestId, decision, reason) => apiRequest(`/api/admin/certificate-requests/${encodeURIComponent(requestId)}/review`, {
    method: 'POST', token, body: { decision, reason: reason || null },
  }),
};
