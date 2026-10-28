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

const ENTITY_TYPE_LABELS = {
  AdminAccount: 'Admin accounts',
  CertificateRequest: 'Certificate requests',
  Course: 'Courses',
  CourseDomain: 'Course domains',
  CourseIntake: 'Intakes',
  CourseMaterialVersion: 'Material versions',
  CourseTrainer: 'Course trainers',
  Dispute: 'Disputes',
  Enrollment: 'Enrollments',
  RoleRequest: 'Role requests',
  User: 'Users',
};

export function entityTypeLabel(entityType) {
  return ENTITY_TYPE_LABELS[entityType] ?? entityType;
}

export function actionLabel(action) {
  return action.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function resultClass(result) {
  const key = String(result || '').toLowerCase();
  return ['approved', 'published', 'succeeded', 'success'].includes(key) ? 'approved'
    : ['rejected', 'failed'].includes(key) ? 'rejected'
      : ['pendingapproval', 'pending'].includes(key) ? 'pendingapproval' : '';
}

export function resultLabel(result) {
  return result === 'PendingApproval' ? 'Pending approval' : result || '—';
}
