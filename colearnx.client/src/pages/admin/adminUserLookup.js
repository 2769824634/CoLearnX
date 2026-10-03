import { adminLaterPhaseApi } from '../../api/adminLaterPhase';

export function loadAdminUsers(token, search, signal) {
  return adminLaterPhaseApi.users(token, search, signal);
}
