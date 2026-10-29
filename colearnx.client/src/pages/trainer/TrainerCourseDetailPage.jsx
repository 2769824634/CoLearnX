import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { courseTrainersApi } from '../../api/courseTrainers';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import TrainerLearnersPage from './TrainerLearnersPage';
import useTrainerQuery from './useTrainerQuery';

async function loadCourseWorkspace(token, courseId, signal) {
  const numericCourseId = Number(courseId);
  const [invitations, summaries] = await Promise.all([
    courseTrainersApi.myInvitations(token, signal),
    trainerIntakesApi.list(token, signal),
  ]);
  const course = invitations.find((item) => item.courseId === numericCourseId && item.status === 'Accepted');
  if (!course) throw { status: 404, code: 'COURSE_NOT_FOUND', message: 'This teaching Course is not available.' };
  const owned = summaries.filter((item) => item.courseId === numericCourseId);
  const intakes = await Promise.all(owned.map((item) => trainerIntakesApi.get(token, item.id, signal)));
  return { course, intakes };
}

export default function TrainerCourseDetailPage() {
  const { courseId } = useParams();
  const query = useTrainerQuery(loadCourseWorkspace, courseId);
  const [learnersOpen, setLearnersOpen] = useState(false);
  return <section className="trainer-page later-page">
    <div className="trainer-detail-nav"><Link className="trainer-back" to="/trainer/courses">← All courses</Link></div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <TrainerHeader eyebrow={query.data.course.courseCode} title={query.data.course.courseTitle}>
        Open a class, then choose the Session where delivery work belongs.
      </TrainerHeader>
      <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Classes</p><h2>Intakes <span className="trainer-count">{query.data.intakes.length}</span></h2></div></div>
      {query.data.intakes.length ? <div className="trainer-sessions">{query.data.intakes.map((intake) => <article className="trainer-session-card" key={intake.id}>
        <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Intake #{intake.id}</p><h3><Link to={`/trainer/courses/intakes/${intake.id}`}>Class #{intake.id}</Link></h3></div><IntakeStatus status={intake.status} /></div>
        {intake.sessions?.length ? <ul className="later-resource-list">{intake.sessions.map((session) => <li key={session.id}><div><strong><Link to={`/trainer/courses/intakes/${intake.id}/sessions/${session.id}`}>{session.label}</Link></strong><small>Session #{session.id}</small></div><Link className="btn btn-ghost" to={`/trainer/courses/intakes/${intake.id}/sessions/${session.id}`}>Open Session</Link></li>)}</ul> : <p className="trainer-help">No Sessions are configured for this Intake.</p>}
      </article>)}</div> : <div className="trainer-empty">No classes have been assigned for this Course.</div>}
      <section className="later-panel">
        <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Course roster</p><h2>Learners & assessments</h2></div>
          <button type="button" className="btn btn-ghost" aria-expanded={learnersOpen} onClick={() => setLearnersOpen((value) => !value)}>{learnersOpen ? 'Hide learner list' : 'Show learner list'}</button>
        </div>
        {learnersOpen ? <TrainerLearnersPage courseId={Number(courseId)} embedded /> : <p className="trainer-help">Expand to manage learners, assessments, grades and certificate reviews.</p>}
      </section>
    </>}
  </section>;
}
