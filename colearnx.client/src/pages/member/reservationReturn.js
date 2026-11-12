const KEY = 'colearnx.reservationReturn';
const MAX_AGE_MS = 2 * 60 * 60 * 1000;

export function validReservationReturn(value, userId) {
  if (!value || !/^\/member\/courses\/\d+$/.test(value.returnTo || '')
    || !Number.isInteger(Number(value.sessionId)) || Number(value.sessionId) <= 0
    || !Number.isFinite(Number(value.need)) || Number(value.need) < 0
    || typeof value.courseTitle !== 'string') return null;
  if (value.userId && userId && value.userId !== userId) return null;
  if (value.savedAt && Date.now() - value.savedAt > MAX_AGE_MS) return null;
  return value;
}

export function saveReservationReturn(value) {
  try { sessionStorage.setItem(KEY, JSON.stringify({ ...value, savedAt: Date.now() })); } catch { /* storage is optional */ }
}

export function readReservationReturn(userId) {
  try { return validReservationReturn(JSON.parse(sessionStorage.getItem(KEY) || 'null'), userId); } catch { return null; }
}

export function clearReservationReturn() {
  try { sessionStorage.removeItem(KEY); } catch { /* storage is optional */ }
}
