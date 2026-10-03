import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import { enrollmentsApi } from '../../api';
import { useMemberData } from './memberDataState';
import { formatUtcDateTime, formatUtcRange, userFacingError } from '../businessPresentation';
import { sessionAvailability } from './sessionAvailability';

// Course detail + enrol.
export default function MemberCourseDetailPage() {
  const { courseId } = useParams();
  const navigate = useNavigate();
  const { state, showToast, loadCourseDetail, enrol, toggleWish } = useMemberData();
  const [courseSnapshot, setCourseSnapshot] = useState(null);
  const currentSnapshot = courseSnapshot?.courseId === courseId ? courseSnapshot : null;
  const course = currentSnapshot?.course;
  const loadError = currentSnapshot?.error || '';
  const [sessionIdx, setSessionIdx] = useState(0);
  const [enrolOpen, setEnrolOpen] = useState(false);
  const [enrolBusy, setEnrolBusy] = useState(false);
  const [insufficientOpen, setInsufficientOpen] = useState(false);
  const [successOpen, setSuccessOpen] = useState(false);
  const [successMsg, setSuccessMsg] = useState('');
  const [ratingOpen, setRatingOpen] = useState(false);
  const [stars, setStars] = useState(5);
  const [comment, setComment] = useState('');
  const [ratingBusy, setRatingBusy] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    let cancelled = false;
    loadCourseDetail(Number(courseId))
      .then((c) => {
        if (!cancelled) {
          setCourseSnapshot({ courseId, course: c });
          const firstOpen = (c.sessions || []).findIndex((item) => sessionAvailability(item, Date.now()).canReserve);
          setSessionIdx(Math.max(0, firstOpen));
        }
      })
      .catch((e) => { if (!cancelled) setCourseSnapshot({ courseId, error: userFacingError(e, 'Could not load this course. Return to the catalog and try again.') }); });
    return () => {
      cancelled = true;
    };
  }, [courseId, loadCourseDetail, showToast]);

  function setCourse(update) {
    setCourseSnapshot((snapshot) => snapshot?.courseId === courseId
      ? { ...snapshot, course: typeof update === 'function' ? update(snapshot.course) : update } : snapshot);
  }

  if (!course) {
    return (
      <MemberShell title="Course" subtitle="Loading…">
        {loadError ? <div role="alert" className="callout warn">{loadError}<button type="button" className="btn btn-ghost" onClick={() => navigate('/member/courses')}>Back to catalog</button></div> : <p className="page-sub" role="status">Loading course detail…</p>}
      </MemberShell>
    );
  }

  const sessions = course.sessions || [];
  const session = sessions[sessionIdx] || sessions[0];
  const afterBalance = state.credits - course.credits;
  const completedEnrollment = (state.enrolled || []).find((item) => item.id === course.id && item.status === 'completed');
  const noPublishedSession = 'This course has no published session yet. A Trainer must open an Intake and the Creator must confirm it.';
  const availability = sessionAvailability(session, now);

  function tryEnrol() {
    if (course.alreadyEnrolled) {
      showToast('Already enrolled');
      return;
    }
    if (!session) {
      showToast(noPublishedSession);
      return;
    }
    const current = sessionAvailability(session, Date.now());
    if (!current.canReserve) { showToast(current.label); return; }
    if (state.credits < course.credits) {
      setInsufficientOpen(true);
      return;
    }
    setEnrolOpen(true);
  }

  async function confirmEnrol() {
    if (enrolBusy) return;
    const current = sessionAvailability(session, Date.now());
    if (!current.canReserve) {
      setEnrolOpen(false);
      showToast(current.label);
      return;
    }
    setEnrolBusy(true);
    try {
      const result = await enrol(course.id, session.id);
      setEnrolOpen(false);
      setSuccessMsg(`${result.creditsSpent} credits on hold · ${result.balance} available · ${result.heldAfter} on hold in total`);
      setSuccessOpen(true);
      setCourse((c) => ({ ...c, alreadyEnrolled: true }));
    } catch (e) {
      setEnrolOpen(false);
      if (e?.code === 'INSUFFICIENT_CREDITS') setInsufficientOpen(true);
      else showToast(userFacingError(e, 'Could not reserve your place. Check My Programs before trying again.'));
    } finally {
      setEnrolBusy(false);
    }
  }

  return (
    <>
      <MemberShell
        title={`${course.code} — ${course.title}`}
      >
        <div className="grid-2-1">
          <div>
            <section className="card member-course-overview" style={{ marginBottom: 16 }}>
              <div className="card-header">About this course</div><div className="card-body">
                <p>{course.description || 'A course description has not been provided yet.'}</p>
                <div className="btn-row"><span className="pill neutral">{course.level || 'Level not specified'}</span><span className="pill neutral">{course.learningPath || 'Learning path not specified'}</span></div>
              </div>
            </section>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Learning Outcomes</div>
              <div className="card-body" style={{ fontSize: 13, lineHeight: 1.8 }}>
                {!(course.outcomes || []).length ? <p>Learning outcomes have not been provided yet.</p> : null}
                {(course.outcomes || []).map((o) => (
                  <div key={o}>• {o}</div>
                ))}
              </div>
            </div>
            <div className="card">
              <div className="card-header">Training Sessions</div>
              <div className="card-body">
                {sessions.length === 0 ? (
                  <p className="page-sub" style={{ margin: 0 }}>{noPublishedSession}</p>
                ) : sessions.map((s, i) => (
                  <button
                    type="button"
                    key={s.id}
                    className={`session-option member-session-option${i === sessionIdx ? ' selected' : ''}`}
                    aria-pressed={i === sessionIdx}
                    onClick={() => setSessionIdx(i)}
                  >
                    <strong>{s.label}</strong> · {s.startsAt && s.endsAt ? formatUtcRange(s.startsAt, s.endsAt) : 'Schedule unavailable'}
                    <span className="seats">{(s.capacity ?? 0) > 0 ? `${s.seats} seats left` : 'Online'}</span>
                    <span className="member-session-status">{sessionAvailability(s, now).label}</span>
                    <span className="member-session-summary">{s.intakeEnrollmentCount ?? '—'} learners reserved or enrolled · Minimum {s.minEnrollment ?? 10}</span>
                    <span className="member-session-summary">Delivery: {s.physical ? 'Physical' : 'Online'}</span>
                    {s.registrationClosesAt ? <span className="member-session-summary">Registration closes {formatUtcDateTime(s.registrationClosesAt)}</span> : null}
                  </button>
                ))}
              </div>
            </div>
          </div>
          <div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Course team</div>
              <div className="card-body">
                <p>Creator</p><strong>{course.creatorName || 'Creator not specified'}</strong>
                <p>Trainer</p>{course.trainerNames?.length ? course.trainerNames.map((name) => <div key={name}>{name}</div>) : <p>A Trainer is assigned when a class opens.</p>}
              </div>
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Course Fee</div>
              <div className="card-body">
                <strong style={{ color: 'var(--purple)', fontSize: 18 }}>{course.credits} Credits</strong>
                <p className="page-sub">Certificate applications require 100% completion, at least 80% attendance and all assessments passed, followed by Trainer and Admin approval.</p>
              </div>
            </div>
            <div className="card" style={{ marginBottom: 12 }}><div className="card-header">Course topics and rating</div>
              <div className="card-body">
                <div>{course.interests?.map((tag) => <span className="pill neutral" key={tag.id} style={{ marginRight: 6 }}>{tag.name}</span>)}</div>
                <p>{course.ratingCount ? `${Number(course.averageStars).toFixed(1)} ★ from ${course.ratingCount} rating(s)` : 'New · No ratings yet'}</p>
                {completedEnrollment ? <button type="button" className="btn btn-ghost btn-sm" onClick={() => setRatingOpen(true)}>Rate this course</button> : null}
              </div>
            </div>
            <button type="button" className="btn btn-ghost btn-block" onClick={() => toggleWish(course.id)}>
              {state.wishlist.includes(course.id) ? '♥ Saved to Wishlist' : '+ Add to Wishlist'}
            </button>
          </div>
        </div>

        <div className="enrol-bar">
          <span className="credits">{course.credits} Credits</span>
          <span style={{ fontSize: 13, color: 'var(--slate)' }}>{course.code}</span>
          <span style={{ flex: 1 }} />
          {course.alreadyEnrolled ? (
            <span className="pill success">Already Enrolled</span>
          ) : (
            <button type="button" className="btn btn-primary" onClick={tryEnrol} disabled={!availability.canReserve}>{availability.canReserve ? 'Reserve place' : availability.label}</button>
          )}
        </div>
      </MemberShell>

      <Modal open={enrolOpen} title="Confirm Enrolment" onClose={() => { if (!enrolBusy) setEnrolOpen(false); }}>
        <div className="card purple-bg" style={{ marginBottom: 12 }}>
          <div className="card-body">
            <strong>{course.code} — {course.title}</strong>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 6 }}>
              {session ? formatUtcRange(session.startsAt, session.endsAt) : ''} · <span style={{ color: 'var(--purple)', fontWeight: 600 }}>{course.credits} Credits</span>
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
          <span style={{ color: 'var(--slate)' }}>Current balance</span>
          <strong style={{ color: 'var(--teal)' }}>{state.credits}</strong>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 12 }}>
          <span style={{ color: 'var(--slate)' }}>Available after hold</span>
          <strong>{afterBalance}</strong>
        </div>
        <p className="page-sub">{course.credits} credits will be held until the class is confirmed at registration close. If it does not run, or you cancel before confirmation, they return to your available balance. The minimum is {session?.minEnrollment ?? 10} learners. After confirmation, withdrawal 6–10 calendar days before the start refunds 70%; within five days it is closed.</p>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" disabled={enrolBusy} onClick={() => setEnrolOpen(false)}>Cancel</button>
          <button type="button" className="btn btn-primary" disabled={enrolBusy || !availability.canReserve} onClick={confirmEnrol}>{enrolBusy ? 'Reserving…' : 'Confirm Enrolment'}</button>
        </div>
      </Modal>

      <Modal open={insufficientOpen} title="Insufficient Credits" onClose={() => setInsufficientOpen(false)} width={440}>
        <div className="callout warn">
          <div className="callout-title">Cannot enrol</div>
          You need {course.credits} credits to enrol. Current balance is {state.credits}.
          Top up in Payment, then return to complete enrolment.
        </div>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={() => setInsufficientOpen(false)}>Cancel</button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              setInsufficientOpen(false);
              navigate('/member/payment');
            }}
          >
            Go to Payment
          </button>
        </div>
      </Modal>

      <Modal open={successOpen} title="Enrolment Successful" onClose={() => setSuccessOpen(false)}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 48, color: 'var(--teal)' }}>✓</div>
          <strong style={{ display: 'block', margin: '8px 0' }}>Your place is reserved</strong>
          <p style={{ fontSize: 13, color: 'var(--slate)' }}>{successMsg}</p>
          <div className="modal-actions">
            <button type="button" className="btn btn-ghost" onClick={() => { setSuccessOpen(false); navigate('/member/courses'); }}>Browse More</button>
            <button type="button" className="btn btn-primary" onClick={() => { setSuccessOpen(false); navigate('/member/programs?tab=reserved'); }}>Go to My Programs</button>
          </div>
        </div>
      </Modal>
      <Modal open={ratingOpen} title="Rate this course" onClose={() => setRatingOpen(false)}>
        <div className="form-group"><label htmlFor="course-rating-stars">Stars</label><select id="course-rating-stars" value={stars} onChange={(event) => setStars(Number(event.target.value))}>{[1, 2, 3, 4, 5].map((value) => <option key={value} value={value}>{value} star{value > 1 ? 's' : ''}</option>)}</select></div>
        <div className="form-group"><label htmlFor="course-rating-comment">Comment (optional)</label><textarea id="course-rating-comment" maxLength={500} value={comment} onChange={(event) => setComment(event.target.value)} /></div>
        <div className="modal-actions"><button type="button" className="btn btn-ghost" onClick={() => setRatingOpen(false)}>Cancel</button>
          <button type="button" className="btn btn-primary" disabled={ratingBusy} onClick={async () => {
            setRatingBusy(true);
            try {
              await enrollmentsApi.rate(completedEnrollment.enrollmentId, stars, comment);
              setCourse(await loadCourseDetail(course.id));
              setRatingOpen(false);
              showToast('Rating saved');
            } catch (error) { showToast(userFacingError(error, 'Could not save your rating. Please try again.')); }
            finally { setRatingBusy(false); }
          }}>{ratingBusy ? 'Saving…' : 'Save rating'}</button></div>
      </Modal>
    </>
  );
}
