import { utcDate } from './utcDates.js';

// Mirrors the server limits in Storage/AssignmentFiles.cs and the MaterialFiles type list.
export const ASSIGNMENT_ACCEPT = '.pdf,.docx,.pptx,.png,.jpg,.jpeg';
export const ASSIGNMENT_MAX_FILES = 5;
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const MAX_TOTAL_BYTES = 25 * 1024 * 1024;
const ALLOWED = /\.(pdf|docx|pptx|png|jpe?g)$/i;

export function formatFileSize(bytes) {
  const value = Number(bytes) || 0;
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

export function fileFormat(name) {
  const extension = String(name || '').split('.').pop().toUpperCase();
  return extension === 'JPEG' ? 'JPG' : extension;
}

// Returns an error message, or '' when the selection can be uploaded.
export function assignmentSelectionError(files) {
  if (!files.length) return 'Choose at least one document.';
  if (files.length > ASSIGNMENT_MAX_FILES) return `You can submit at most ${ASSIGNMENT_MAX_FILES} documents.`;
  const wrongType = files.find((file) => !ALLOWED.test(file.name));
  if (wrongType) return `${wrongType.name} is not supported. Use PDF, DOCX, PPTX, PNG or JPG.`;
  const empty = files.find((file) => !file.size);
  if (empty) return `${empty.name} is empty.`;
  const tooBig = files.find((file) => file.size > MAX_FILE_BYTES);
  if (tooBig) return `${tooBig.name} is larger than 10 MB.`;
  if (files.reduce((sum, file) => sum + file.size, 0) > MAX_TOTAL_BYTES) return 'All documents together must be 25 MB or smaller.';
  return '';
}

// "Due in 3 days" style label; deadline comparisons use the browser clock, the server re-checks on submit.
export function deadlineState(dueAt, now = Date.now()) {
  if (!dueAt) return { tone: '', label: 'No deadline set' };
  const due = utcDate(dueAt).getTime();
  if (!Number.isFinite(due)) return { tone: '', label: 'Deadline unavailable' };
  const left = due - now;
  if (left <= 0) return { tone: 'closed', label: 'Submission closed', closed: true };
  const hours = Math.floor(left / 3600000);
  if (hours < 1) return { tone: 'soon', label: `Closes in ${Math.max(1, Math.ceil(left / 60000))} min` };
  if (hours < 48) return { tone: 'soon', label: `Closes in ${hours} h` };
  return { tone: '', label: `Closes in ${Math.floor(hours / 24)} days` };
}
