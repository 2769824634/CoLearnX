import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { SessionForm } from '../trainer/IntakeForms';
import { formatDate, statusLabel } from '../trainer/intakeForm';
import TrainerResourcesPanel from '../trainer/TrainerResourcesPanel';
import TrainerSessionMaterialsPanel from '../trainer/TrainerSessionMaterialsPanel';
import { staffLaterPhaseApi } from '../../api/trainerLaterPhase';
import TeachingDatesEditor from '../account/TeachingDatesEditor';

export default function StaffIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const { token } = useStaffAuth();
  const [intake, setIntake] = useState(null);
  const [editor, setEditor] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    staffCoursesApi.getIntake(token, courseIntakeId, controller.signal).then(setIntake)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure); });
    return () => controller.abort();
  }, [courseIntakeId, token]);
  async function addSession(body) {
    setBusy(true); setError(null);
    try { setIntake(await staffCoursesApi.createSession(token, intake.id, body)); setEditor(false); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  async function publish() {
    setBusy(true); setError(null);
    try { setIntake(await staffCoursesApi.publishIntake(token, intake.id, intake.version)); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  if (!intake) return <section className="creator-page">{error ? <div className="callout warn">{error.message}</div> : 'Loading class…'}</section>;
  const publishable = ['Draft', 'Rejected'].includes(intake.status);
  return <section className="creator-page"><Link className="creator-back" to="/staff/intakes">← All classes</Link>
    <div className="page-header"><div><p className="eyebrow">Class #{intake.id}</p><h1>Platform class schedule</h1><p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p></div><span className={`creator-status ${intake.status.toLowerCase()}`}>{statusLabel(intake.status)}</span></div>
    {error ? <div className="callout warn">{error.message}</div> : null}
    <TeachingDatesEditor intake={intake} save={(body) => staffCoursesApi.updateTeachingDates(token, intake.id, body)}
      reload={() => staffCoursesApi.getIntake(token, intake.id)} onSaved={setIntake} />
    <div className="creator-section-heading"><h2>Sessions</h2>{publishable && !editor ? <button className="btn btn-primary" type="button" onClick={() => setEditor(true)}>Add session</button> : null}</div>
    {editor ? <SessionForm intake={intake} onSave={addSession} onCancel={() => setEditor(false)} busy={busy} error={error} onError={setError} /> : null}
    {(intake.sessions || []).map((session) => <article className="creator-application-row" key={session.id}><div><h2>{session.label}</h2><p>{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>{session.meetingLink ? <p>{session.meetingLink}</p> : null}</div></article>)}
    {['Published', 'InProgress'].includes(intake.status) ? <TrainerResourcesPanel token={token} intake={intake} api={staffLaterPhaseApi} /> : null}
    {['Published', 'InProgress'].includes(intake.status) ? <TrainerSessionMaterialsPanel token={token} intake={intake} api={staffLaterPhaseApi} /> : null}
    {publishable ? <div className="creator-section-heading"><div><h2>Publish this class</h2><p>Publishing opens registration to members.</p></div><button className="btn btn-primary" type="button" disabled={busy || !intake.sessions?.length} onClick={publish}>Publish</button></div> : null}
  </section>;
}
