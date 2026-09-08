import { useSearchParams } from 'react-router-dom';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';

const QUEUES = [
  { id: 'roles', label: 'Role requests', detail: 'Trainer & Creator' },
  { id: 'courses', label: 'Course reviews', detail: 'Publish & reject' },
];

export default function AdminApprovalsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeQueue = searchParams.get('queue') === 'courses' ? 'courses' : 'roles';

  return (
    <div className="admin-approval-desk">
      <div className="admin-approval-switch" role="group" aria-label="Approval queues">
        <div className="admin-approval-switch-label">
          <span>Approval desk</span>
          <small>02 governed queues</small>
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
        {activeQueue === 'roles' ? <AdminRoleRequestsPage /> : <AdminCourseReviewsPage />}
      </div>
    </div>
  );
}
