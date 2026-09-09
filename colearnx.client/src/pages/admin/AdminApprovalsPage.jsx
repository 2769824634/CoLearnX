import { useSearchParams } from 'react-router-dom';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';
import AdminLaterApprovalsPage from './AdminLaterApprovalsPage';

const QUEUES = [
  { id: 'roles', label: 'Role requests', detail: 'Trainer & Creator' },
  { id: 'courses', label: 'Course reviews', detail: 'Publish & reject' },
  { id: 'materials', label: 'Material versions', detail: 'Approve Trainer use' },
  { id: 'certificates', label: 'Certificates', detail: 'Final issuance gate' },
];

export default function AdminApprovalsPage() {
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
        {QUEUES.map((queue, index) => (
          <button
            key={queue.id}
            id={`approval-tab-${queue.id}`}
            type="button"
            aria-pressed={activeQueue === queue.id}
            aria-controls={`approval-panel-${queue.id}`}
            className={activeQueue === queue.id ? 'active' : ''}
            onClick={() => setSearchParams({ queue: queue.id })}
          >
            <span>0{index + 1}</span>
            <strong>{queue.label}</strong>
            <small>{queue.detail}</small>
          </button>
        ))}
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
