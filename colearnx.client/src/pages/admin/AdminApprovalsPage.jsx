import { useSearchParams } from 'react-router-dom';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';
import AdminLaterApprovalsPage from './AdminLaterApprovalsPage';
import { useAdminPending } from './adminPending';

const QUEUES = [
  { id: 'roles', label: 'Role requests', detail: 'Trainer & Creator', countKey: 'roleRequests' },
  { id: 'courses', label: 'Course reviews', detail: 'Publish & reject', countKey: 'courses' },
  { id: 'materials', label: 'Material versions', detail: 'Approve Trainer use', countKey: 'materialVersions' },
  { id: 'certificates', label: 'Certificates', detail: 'Final issuance gate', countKey: 'certificates' },
];

export default function AdminApprovalsPage() {
  const counts = useAdminPending()?.counts;
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedQueue = searchParams.get('queue');
  const activeQueue = QUEUES.some((queue) => queue.id === requestedQueue) ? requestedQueue : 'roles';

  return (
    <div className="admin-approval-desk">
      <div className="admin-approval-switch" role="group" aria-label="Approval queues">
        <div className="admin-approval-switch-label">
          <span>Approval desk</span>
          <small>04 governed queues</small>
        </div>
        {QUEUES.map((queue, index) => {
          const pending = counts?.[queue.countKey] ?? 0;
          return (
            <button
              key={queue.id}
              id={`approval-tab-${queue.id}`}
              type="button"
              aria-pressed={activeQueue === queue.id}
              aria-controls={`approval-panel-${queue.id}`}
              aria-label={pending > 0 ? `${queue.label}, ${pending} pending` : undefined}
              className={activeQueue === queue.id ? 'active' : ''}
              onClick={() => setSearchParams({ queue: queue.id })}
            >
              <span>0{index + 1}</span>
              <strong>{queue.label}</strong>
              <small>{queue.detail}</small>
              {pending > 0 ? <em className="admin-approval-count" aria-hidden="true">{pending > 99 ? '99+' : pending}</em> : null}
            </button>
          );
        })}
      </div>

      <div
        id={`approval-panel-${activeQueue}`}
        role="region"
        aria-labelledby={`approval-tab-${activeQueue}`}
      >
        {activeQueue === 'roles' ? <AdminRoleRequestsPage /> : null}
        {activeQueue === 'courses' ? <AdminCourseReviewsPage /> : null}
        {activeQueue === 'materials' ? <AdminLaterApprovalsPage type="materials" /> : null}
        {activeQueue === 'certificates' ? <AdminLaterApprovalsPage type="certificates" /> : null}
      </div>
    </div>
  );
}
