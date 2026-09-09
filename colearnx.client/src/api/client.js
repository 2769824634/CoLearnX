const TOKEN_KEY = 'colearnx.token';
const ADMIN_TOKEN_KEY = 'colearnx.admin.token';

export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export function getStoredAdminToken() {
  return localStorage.getItem(ADMIN_TOKEN_KEY);
}

export function setStoredAdminToken(token) {
  if (token) localStorage.setItem(ADMIN_TOKEN_KEY, token);
  else localStorage.removeItem(ADMIN_TOKEN_KEY);
}

export class ApiError extends Error {
  constructor(code, message, status, fieldErrors = {}) {
    super(message);
    this.code = code;
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

// Fetch helper for /api/*. Shared: apiRequest, ApiError
export async function apiRequest(path, { method = 'GET', body, token, signal } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const auth = token ?? getStoredToken();
  if (auth) headers.Authorization = `Bearer ${auth}`;

  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  });

  if (res.status === 204) return null;

  const text = await res.text();
  let data;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    throw new ApiError('INVALID_RESPONSE', 'The server returned an unreadable response. Please try again.', res.status);
  }

  if (!res.ok) {
    throw new ApiError(data?.code || 'HTTP_ERROR', data?.message || data?.title || res.statusText, res.status, data?.fieldErrors ?? {});
  }
  return data;
}
