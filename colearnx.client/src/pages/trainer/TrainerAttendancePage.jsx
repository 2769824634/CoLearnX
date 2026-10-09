import { useMemo, useState } from 'react';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import useTrainerQuery from './useTrainerQuery';
import { formatUtcRange } from '../businessPresentation';

function sessionAttendanceStatus(learner, sessionId) {
  return learner.sessionAttendances?.find((item) => item.courseSessionId === sessionId)?.status;
}

async function loadAttendance(token, _key, signal) {
  const [summaries, catalog] = await Promise.all([
    trainerIntakesApi.list(token, signal), trainerIntakesApi.publishedCourses(token, signal),
  ]);
  const cohorts = await Promise.all(summaries.map(async (summary) => {
    const [intake, learners] = await Promise.all([
      trainerIntakesApi.get(token, summary.id, signal),
      trainerLaterPhaseApi.learners(token, summary.id, signal),
    ]);
    const course = catalog.find((item) => item.id === intake.courseId);
    return { ...intake, learners, courseCode: course?.code, courseTitle: course?.title };
  }));
  return cohorts;
}

export default function TrainerAttendancePage() {
  const query = useTrainerQuery(loadAttendance);
  const [intakeChoice, setIntakeChoice] = useState('');
  const [sessionChoice, setSessionChoice] = useState('');
  const [draft, setDraft] = useState({});
  const [saving, setSaving] = useState(false);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const intake = query.data?.find((item) => String(item.id) === intakeChoice) || query.data?.[0];
  const session = intake?.sessions.find((item) => String(item.id) === sessionChoice) || intake?.sessions[0];
  const learners = useMemo(() => intake?.learners.filter((item) =>
    ['Active', 'Completed'].includes(item.enrollmentStatus)) || [], [intake]);

  async function saveAttendance() {
    if (!intake || !session || !learners.length || saving) return;
    setSaving(true);
    setMutationError(null);
    setNotice('');
    try {
      const records = learners.map((learner) => ({
        enrollmentId: learner.enrollmentId,
        status: draft[learner.enrollmentId] || sessionAttendanceStatus(learner, session.id) || 'Present',
      }));
      await trainerLaterPhaseApi.saveAttendance(query.token, intake.id, session.id, records);
      setDraft({});
      setNotice(`Attendance saved for ${records.length} learner${records.length === 1 ? '' : 's'}.`);
      query.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setSaving(false);
    }
  }

  return <section className="trainer-page later-page">
    <TrainerHeader title="Attendance tracking">Select an Intake and Session, then save one clear attendance state for each enrolled learner.</TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <div className="trainer-toolbar later-toolbar">
        <div className="form-group"><label htmlFor="attendance-intake">Intake</label><select id="attendance-intake" value={intake?.id || ''} onChange={(event) => { setIntakeChoice(event.target.value); setSessionChoice(''); setDraft({}); }}>
          {query.data.map((item) => <option key={item.id} value={item.id}>{item.courseCode || `Course #${item.courseId}`} — {item.courseTitle || 'Course'} · Intake #{item.id} · {item.status}</option>)}
        </select></div>
        <div className="form-group"><label htmlFor="attendance-session">Session</label><select id="attendance-session" value={session?.id || ''} onChange={(event) => { setSessionChoice(event.target.value); setDraft({}); }}>
          {(intake?.sessions || []).map((item) => <option key={item.id} value={item.id}>{item.label} · {formatUtcRange(item.startsAt, item.endsAt)}</option>)}
        </select></div>
      </div>
      {intake && session ? <p>{intake.courseCode || `Course #${intake.courseId}`} — {intake.courseTitle || 'Course'} · Intake #{intake.id} · {session.label} · {formatUtcRange(session.startsAt, session.endsAt)}</p> : null}
      {notice ? <p className="trainer-notice" role="status">{notice}</p> : null}
      <TrainerError error={mutationError} />
      {!query.data.length ? <div className="trainer-empty">No owned Intakes are available.</div> : !session ? <div className="trainer-empty">This Intake has no Sessions.</div> : !learners.length ? <div className="trainer-empty">No active or completed learners are enrolled in this Intake.</div> : <>
        <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Learner</th><th>Enrollment</th><th>Progress</th><th>Attendance</th></tr></thead><tbody>
          {learners.map((learner) => <tr key={learner.enrollmentId}><td><strong>{learner.fullName}</strong><small>{learner.email}</small></td><td>#{learner.enrollmentId}</td><td>{learner.progressPercent}%</td><td><select aria-label={`Attendance for ${learner.fullName}`} value={draft[learner.enrollmentId] || sessionAttendanceStatus(learner, session.id) || 'Present'} onChange={(event) => setDraft((current) => ({ ...current, [learner.enrollmentId]: event.target.value }))}><option>Present</option><option>Late</option><option>Absent</option></select></td></tr>)}
        </tbody></table></div>
        <div className="later-actions"><span>{session.label} · {learners.length} learner{learners.length === 1 ? '' : 's'}</span><button className="btn btn-primary" type="button" disabled={saving} onClick={saveAttendance}>{saving ? 'Saving…' : 'Save attendance'}</button></div>
      </>}
    </>}
  </section>;
}
