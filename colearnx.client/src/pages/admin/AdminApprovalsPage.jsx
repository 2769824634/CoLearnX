import { useSearchParams } from 'react-router-dom';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';
import AdminLaterApprovalsPage from './AdminLaterApprovalsPage';
import AdminDisputesPage from './AdminDisputesPage';
import AdminPayoutsPage from './AdminPayoutsPage';
import { useAdminPending } from './adminPending';

const QUEUES = [
  { id: 'roles', label: 'Role requests', detail: 'Trainer & Creator', countKey: 'roleRequests' },
  { id: 'courses', label: 'Course reviews', detail: 'Courses & their files', countKey: 'courses' },
  { id: 'certificates', label: 'Certificates', detail: 'Final issuance gate', countKey: 'certificates' },
  { id: 'disputes', label: 'Disputes', detail: 'Refund or reject', countKey: 'disputes' },
  { id: 'payouts', label: 'Payouts', detail: 'Pay Trainers & Creators', countKey: 'payouts' },
];

export default function AdminApprovalsPage() {
  const counts = useAdminPending()?.counts;
  const [searchParams, setSearchParams] = useSearchParams();
  // Files are reviewed with their course, so old links to the material queue land on course reviews.
  const requestedQueue = searchParams.get('queue') === 'materials' ? 'courses' : searchParams.get('queue');
  const activeQueue = QUEUES.some((queue) => queue.id === requestedQueue) ? requestedQueue : 'roles';

  return (
    <div className="admin-approval-desk">
      <div className="admin-approval-switch" role="group" aria-label="Approval queues">
        <div className="admin-approval-switch-label">
          <span>Approval desk</span>
          <small>0{QUEUES.length} governed queues</small>
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
        {activeQueue === 'certificates' ? <AdminLaterApprovalsPage /> : null}
        {activeQueue === 'disputes' ? <AdminDisputesPage /> : null}
        {activeQueue === 'payouts' ? <AdminPayoutsPage /> : null}
      </div>
    </div>
  );
}
