const dateFormatter = new Intl.DateTimeFormat('en-AU', {
  dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC',
});

export function formatAuditTime(value) {
  if (!value) return '—';
  // SQLite returns UTC DateTime values without a suffix; keep the displayed timezone explicit.
  const date = new Date(/(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? '—' : `${dateFormatter.format(date)} UTC`;
}

export function auditActor(log) {
  return log.adminAccountId != null ? `Admin #${log.adminAccountId}` : `User #${log.userId}`;
}

export function actionLabel(action) {
  return action.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function resultClass(result) {
  const key = String(result || '').toLowerCase();
  return ['approved', 'published', 'succeeded'].includes(key) ? 'approved'
    : ['rejected', 'failed'].includes(key) ? 'rejected'
      : ['pendingapproval', 'pending'].includes(key) ? 'pendingapproval' : '';
}

export function resultLabel(result) {
  return result === 'PendingApproval' ? 'Pending approval' : result || '—';
}
