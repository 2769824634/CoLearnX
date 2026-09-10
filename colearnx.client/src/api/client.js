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
export async function apiRequest(path, { method = 'GET', body, token, signal, asForm = false } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined && !asForm) headers['Content-Type'] = 'application/json';
  const auth = token ?? getStoredToken();
  if (auth) headers.Authorization = `Bearer ${auth}`;

  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : asForm ? body : JSON.stringify(body),
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

export async function downloadFile(path, fileName, token) {
  const headers = { Accept: '*/*' };
  const auth = token ?? getStoredToken();
  if (auth) headers.Authorization = `Bearer ${auth}`;

  const res = await fetch(path, { headers });
  if (!res.ok) {
    let message = res.statusText;
    try {
      const data = await res.json();
      message = data?.message || message;
    } catch {
      /* not JSON */
    }
    throw new ApiError('DOWNLOAD_FAILED', message, res.status);
  }

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName || 'material';
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
