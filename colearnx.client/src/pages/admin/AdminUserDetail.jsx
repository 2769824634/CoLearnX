import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { adminOverviewApi } from '../../api';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import useCachedQuery from '../../api/useCachedQuery';
import useAdminAuth from '../../auth/useAdminAuth';
import AdminDataState from './AdminDataState';
import AdminCreditLedgerPage from './AdminCreditLedgerPage';
import AdminClassTable from './AdminClassTable';
import { attendanceText, classDates, isLowAttendance } from './adminClasses';
import { formatCount, formatUtcDate, formatUtcDateTime } from '../businessPresentation';

const loadUser = (token, id, signal) => adminLaterPhaseApi.user(token, id, signal);
const loadActivity = (token, id, signal) => adminOverviewApi.userActivity(token, id, signal);

const ROLE_PILL = { Member: 'warn', Trainer: 'info', Creator: 'success' };
const ROLE_TABS = { Trainer: 'Teaching', Creator: 'Courses created', Member: 'Learning' };
const FINISHED = new Set(['Completed', 'Cancelled', 'Rejected']);
const COURSE_STATUS = { PendingApproval: 'Pending approval', NeedsChanges: 'Needs changes' };
const LEARNING_STATUS = { Active: 'Enrolled', Reserved: 'Reserved', Completed: 'Completed', Cancelled: 'Cancelled', Refunded: 'Refunded' };

function TeachingTab({ activity, openClass }) {
  const current = activity.teachingClasses.filter((row) => !FINISHED.has(row.status));
  const finished = activity.teachingClasses.filter((row) => FINISHED.has(row.status));
  return <>
    <h4 className="admin-detail-heading">Intakes this Trainer is teaching</h4>
    <AdminClassTable rows={current} onOpen={openClass} hideTrainer emptyText="Not teaching any intake right now." />
    <details className="admin-collapse">
      <summary>Finished and cancelled intakes ({finished.length})</summary>
      <AdminClassTable rows={finished} onOpen={openClass} hideTrainer emptyText="No finished intakes." />
    </details>
  </>;
}

function CreatorTab({ activity, openClass }) {
  const courses = activity.createdCourses;
  return <>
    <dl className="admin-summary-stats">
      <div><dt>Courses</dt><dd>{courses.length}</dd></div>
      <div><dt>Intakes run</dt><dd>{courses.reduce((sum, course) => sum + course.classCount, 0)}</dd></div>
      <div><dt>Learners in total</dt><dd>{courses.reduce((sum, course) => sum + course.learnerCount, 0)}</dd></div>
      <div><dt>Royalty income</dt><dd>{formatCount(activity.royaltyCredits, 'credit')}</dd></div>
    </dl>
    <h4 className="admin-detail-heading">Courses created</h4>
    {courses.length ? <div className="admin-course-list">{courses.map((course) => <details key={course.courseId} className="admin-collapse">
      <summary>
        <strong>{course.title}</strong>
        <span>{course.code}</span>
        <span className="pill neutral">{COURSE_STATUS[course.status] || course.status}</span>
        <span>{formatCount(course.classCount, 'intake', 'intakes')} · {formatCount(course.learnerCount, 'learner')}</span>
      </summary>
      <AdminClassTable rows={course.classes} onOpen={openClass} hideCreator emptyText="No intakes opened for this course yet." />
    </details>)}</div> : <p className="admin-review-state">This Creator has not created a course yet.</p>}
  </>;
}

function LearningTab({ activity, openClass }) {
  const learning = activity.learning;
  const current = learning.filter((item) => item.status === 'Active' || item.status === 'Reserved').length;
  const done = learning.filter((item) => item.status === 'Completed').length;
  return <>
    <dl className="admin-summary-stats">
      <div><dt>Studying now</dt><dd>{current}</dd></div>
      <div><dt>Completed</dt><dd>{done}</dd></div>
      <div><dt>Total topped up</dt><dd>{formatCount(activity.totalTopUpCredits, 'credit')}</dd></div>
      <div><dt>Total spent on courses</dt><dd>{formatCount(activity.totalSpentCredits, 'credit')}</dd></div>
    </dl>
    <h4 className="admin-detail-heading">Courses</h4>
    <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Course</th><th>Intake dates</th><th>Status</th><th>Attendance</th><th>Credits</th></tr></thead>
      <tbody>{learning.map((item) => <tr key={item.enrollmentId} className={isLowAttendance(item.attendanceRate) ? 'admin-class-low' : undefined}>
        <td>{item.classId
          ? <button type="button" className="account-name-button" onClick={() => openClass(item.classId)}>{item.courseTitle}</button>
          : <strong>{item.courseTitle}</strong>}<small>{item.courseCode}{item.classId ? ` · Intake #${item.classId}` : ''}</small></td>
        <td>{classDates(item).start} → {classDates(item).end}</td>
        <td>{LEARNING_STATUS[item.status] || item.status}</td>
        <td>{attendanceText(item.attendanceRate)}<small>{item.attended} of {item.sessionsHeld} sessions</small></td>
        <td>{item.creditsSpent}</td>
      </tr>)}</tbody></table>
      {!learning.length ? <div className="admin-review-state">Has not joined a course yet.</div> : null}</div>
    <h4 className="admin-detail-heading">Certificates</h4>
    {activity.certificates.length
      ? <ul className="admin-material-list">{activity.certificates.map((item) => <li key={item.id}><strong>{item.title}</strong><span>{item.courseCode ? `${item.courseCode} · ` : ''}{formatUtcDate(item.awardedAt)}</span></li>)}</ul>
      : <p className="admin-review-state">No certificates yet.</p>}
  </>;
}

function ProfileTab({ user }) {
  const fields = {
    'Display name': user.displayName, 'User ID': user.id, 'Phone': user.phone, 'Country': user.country,
    'Occupation': user.occupation, 'Status': user.isActive ? 'Active' : 'Inactive',
    'Email verified': user.emailVerifiedAt ? 'Yes' : 'No', 'Joined (UTC)': formatUtcDateTime(user.createdAt),
    'Bio': user.bio, 'Learning goals': user.learningGoals, 'Trainer headline': user.trainerHeadline,
    'Specialisations': user.specialisations, 'Creator headline': user.creatorHeadline, 'Expertise': user.expertiseTags,
  };
  return <dl className="account-detail-grid">
    {Object.entries(fields).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? '—'}</dd></div>)}
  </dl>;
}

export default function AdminUserDetail({ userId, onBalanceChange }) {
  const { token } = useAdminAuth();
  const navigate = useNavigate();
  const query = useCachedQuery(token, loadUser, String(userId));
  const activityQuery = useCachedQuery(token, loadActivity, String(userId));
  const user = query.data;
  const activity = activityQuery.data;
  const roles = activity?.roles?.length ? activity.roles : user?.roles || [];
  const roleTabs = ['Trainer', 'Creator', 'Member'].filter((role) => roles.includes(role));
  const tabs = [...roleTabs, 'Credits', 'Profile'];
  const [chosen, setChosen] = useState('');
  const tab = tabs.includes(chosen) ? chosen : tabs[0];
  const openClass = (id) => navigate(`/admin/classes?classId=${id}`);

  function refreshBalances(result) {
    query.setData({ ...user, creditBalance: result.balanceAfter });
    onBalanceChange?.();
  }

  const teachingRole = roles.find((role) => role === 'Trainer' || role === 'Creator');
  return <div className="account-details">
    {query.error?.status === 404 ? <p role="alert">User was not found.</p> : <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />}
    {user ? <>
      <div className="admin-user-summary">
        <div>
          <h3>{user.fullName}</h3>
          <p>{user.email} · #{user.id}</p>
          {activity?.specialties?.length ? <p className="admin-user-tags">Specialties: {activity.specialties.join(', ')}</p> : null}
        </div>
        <span className="admin-role-pills">
          {roles.map((role) => <span key={role} className={`pill ${ROLE_PILL[role] || 'neutral'}`}>{role}</span>)}
          {teachingRole && activity ? <span className={`pill ${activity.certified ? 'success' : 'warn'}`}>{activity.certified ? 'Certified' : 'Not certified'}</span> : null}
          {user.isActive ? null : <span className="pill neutral">Inactive</span>}
        </span>
      </div>
      <dl className="admin-summary-stats">
        <div><dt>Available credits</dt><dd>{user.creditBalance}</dd></div>
        <div><dt>Credits on hold</dt><dd>{user.heldCredits}</dd></div>
        <div><dt>Joined</dt><dd>{formatUtcDate(user.createdAt)}</dd></div>
        <div><dt>Email verified</dt><dd>{user.emailVerifiedAt ? 'Yes' : 'No'}</dd></div>
      </dl>
      <div className="admin-user-tabs" role="group" aria-label="User detail sections">
        {tabs.map((item) => <button key={item} type="button" className="btn btn-ghost btn-sm" aria-pressed={tab === item} onClick={() => setChosen(item)}>
          {ROLE_TABS[item] || item}
        </button>)}
      </div>
      {roleTabs.includes(tab) ? <AdminDataState loading={activityQuery.loading} error={activityQuery.error} onRetry={activityQuery.refresh} /> : null}
      {tab === 'Trainer' && activity ? <TeachingTab activity={activity} openClass={openClass} /> : null}
      {tab === 'Creator' && activity ? <CreatorTab activity={activity} openClass={openClass} /> : null}
      {tab === 'Member' && activity ? <LearningTab activity={activity} openClass={openClass} /> : null}
      {tab === 'Profile' ? <ProfileTab user={user} /> : null}
    </> : null}
    {user && !query.error && tab === 'Credits' ? <AdminCreditLedgerPage key={userId} userId={userId} onBalanceChange={refreshBalances} /> : null}
  </div>;
}

