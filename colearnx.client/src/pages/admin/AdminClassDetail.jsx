import { useState } from 'react';
import { adminCourseReviewsApi, adminOverviewApi } from '../../api';
import useCachedQuery from '../../api/useCachedQuery';
import useAdminAuth from '../../auth/useAdminAuth';
import IntakeTeachingDatesEditor from '../../components/IntakeTeachingDatesEditor';
import { canEditTeachingDates } from '../../utils/intakeTeachingDates';
import { formatUtcDateTime } from '../businessPresentation';
import AdminDataState from './AdminDataState';
import {
  attendanceText, classStatusLabel, classStatusPill, classDates, isLowAttendance, timeLeftText,
} from './adminClasses';

const loadClass = (token, id, signal) => adminOverviewApi.classDetail(token, id, signal);

const ENROLLMENT_LABELS = { Active: 'Enrolled', Reserved: 'Reserved', Completed: 'Completed', Cancelled: 'Cancelled', Refunded: 'Refunded' };
const CERTIFICATE_LABELS = {
  Submitted: 'Waiting for Trainer',
  TrainerApproved: 'Waiting for admin',
  TrainerRejected: 'Rejected by Trainer',
  AdminRejected: 'Rejected by admin',
  Issued: 'Issued',
};

export default function AdminClassDetail({ classId, onChanged }) {
  const { token } = useAdminAuth();
  const query = useCachedQuery(token, loadClass, String(classId));
  const [editing, setEditing] = useState(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState(null);
  const detail = query.data;
  const row = detail?.class;

  async function openDates() {
    setError(null); setMessage('');
    try { setEditing(await adminCourseReviewsApi.getIntake(token, classId)); }
    catch (failure) { setError(failure); }
  }

  return <div className="account-details admin-class-detail">
    {query.error?.status === 404 ? <p role="alert">Intake was not found.</p> : <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />}
    {row ? <>
      <div className="admin-user-summary">
        <div>
          <h3>{row.courseTitle}</h3>
          <p>{row.courseCode} · Intake #{row.id} · Creator {row.creatorName} · Trainer {row.trainerName}</p>
        </div>
        <span className={classStatusPill(row.status)}>{classStatusLabel(row.status)}</span>
      </div>
      <dl className="admin-summary-stats">
        <div><dt>Dates</dt><dd>{classDates(row).start} → {classDates(row).end}</dd></div>
        <div><dt>Time left</dt><dd>{timeLeftText(row)}</dd></div>
        <div><dt>Learners</dt><dd>{row.learners} <small>(min. {row.minEnrollment})</small></dd></div>
        <div><dt>Attendance</dt><dd className={isLowAttendance(row.attendanceRate) ? 'later-negative' : undefined}>{attendanceText(row.attendanceRate)}</dd></div>
      </dl>
      {canEditTeachingDates(row.status)
        ? <div className="later-actions"><span>Need to move the intake? Sessions stay where they are.</span><button type="button" className="btn btn-ghost btn-sm" onClick={openDates}>Edit teaching dates</button></div>
        : null}
      {error ? <div className="callout warn" role="alert">{error.message}</div> : null}
      {message ? <p role="status">{message}</p> : null}

      <h4 className="admin-detail-heading">Sessions</h4>
      <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Session</th><th>Time (UTC)</th><th>Attended</th></tr></thead>
        <tbody>{detail.sessions.map((session) => <tr key={session.id}>
          <td><strong>{session.label}</strong></td>
          <td>{formatUtcDateTime(session.startsAt)}</td>
          <td>{session.held ? `${session.attended} of ${session.learners}` : 'Not held yet'}</td>
        </tr>)}</tbody></table>
        {!detail.sessions.length ? <div className="admin-review-state">No sessions yet.</div> : null}</div>

      <h4 className="admin-detail-heading">Learners</h4>
      <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Learner</th><th>Place</th><th>Attendance</th><th>Certificate</th></tr></thead>
        <tbody>{detail.learners.map((learner) => <tr key={`${learner.userId}-${learner.enrollmentStatus}`} className={isLowAttendance(learner.attendanceRate) ? 'admin-class-low' : undefined}>
          <td><strong>{learner.fullName}</strong><small>{learner.email}</small></td>
          <td>{ENROLLMENT_LABELS[learner.enrollmentStatus] || learner.enrollmentStatus}</td>
          <td>{attendanceText(learner.attendanceRate)}<small>{learner.attended} of {learner.sessionsHeld} sessions</small></td>
          <td>{learner.certificateStatus ? CERTIFICATE_LABELS[learner.certificateStatus] || learner.certificateStatus : 'Not requested'}</td>
        </tr>)}</tbody></table>
        {!detail.learners.length ? <div className="admin-review-state">No learners yet.</div> : null}</div>

      <h4 className="admin-detail-heading">Materials</h4>
      {detail.materials.length
        ? <ul className="admin-material-list">{detail.materials.map((material, index) => <li key={`${material.title}-${index}`}><strong>{material.title}</strong><span>{material.scope}</span></li>)}</ul>
        : <p className="admin-review-state">No materials attached yet.</p>}
    </> : null}
    {editing ? <IntakeTeachingDatesEditor key={`${editing.id}-${editing.version}`} intake={editing}
      onSave={async (body) => { await adminCourseReviewsApi.updateTeachingDates(token, editing.id, body); query.refresh(); onChanged?.(); }}
      onReload={async () => { setEditing(await adminCourseReviewsApi.getIntake(token, editing.id)); query.refresh(); }}
      onClose={(saved) => { setEditing(null); if (saved) setMessage('Teaching dates saved.'); }} /> : null}
  </div>;
}
