import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import { enrollmentsApi } from '../../api';
import { useMemberSlices } from './memberDataState';
import { formatCount, formatUtcDate, formatUtcRange, userFacingError } from '../businessPresentation';
import { sessionAvailability } from './sessionAvailability';
import { CourseArt } from '../guest/GuestCourseCard';
import { saveReservationReturn } from './reservationReturn';

function initials(name) {
  return (name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase();
}

const currentAvailability = (session) => sessionAvailability(session, Date.now());

// Course detail + enrol.
export default function MemberCourseDetailPage() {
  const { courseId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { state, showToast, loadCourseDetail, peekCourseDetail, enrol, toggleWish } = useMemberSlices('enrollments');
  const numericCourseId = Number(courseId);
  const peeked = peekCourseDetail?.(numericCourseId) || null;
  const [courseSnapshot, setCourseSnapshot] = useState(() => (peeked ? { courseId, course: peeked } : null));
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
  const [deliveryModes, setDeliveryModes] = useState({});

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const cached = peekCourseDetail?.(numericCourseId);
    if (cached) {
      setCourseSnapshot({ courseId, course: cached });
      const firstOpen = (cached.sessions || []).findIndex((item) => sessionAvailability(item, Date.now()).canReserve);
      setSessionIdx(Math.max(0, firstOpen));
      return undefined;
    }
    let cancelled = false;
    loadCourseDetail(numericCourseId)
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
  }, [courseId, loadCourseDetail, numericCourseId, peekCourseDetail, showToast]);

  useEffect(() => {
    if (!location.state?.resumeReserve || !course) return undefined;
    let active = true;
    queueMicrotask(() => {
      if (!active) return;
      const index = (course.sessions || []).findIndex((item) => item.id === Number(location.state.sessionId));
      if (index >= 0 && sessionAvailability(course.sessions[index], Date.now()).canReserve
        && !course.alreadyEnrolled && state.credits >= course.credits) {
        setSessionIdx(index);
        setEnrolOpen(true);
      } else {
        showToast('Please check the class and your balance before reserving.');
      }
      navigate(location.pathname, { replace: true, state: null });
    });
    return () => { active = false; };
  }, [course, location, navigate, showToast, state.credits]);

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
  const classSessions = session?.intakeId ? sessions.filter((item) => item.intakeId === session.intakeId) : [session].filter(Boolean);
  const hasSessionChoices = Boolean(session?.intakeId);
  const physicalAvailable = (item) => item.physical && Number(item.seats) > 0
    && (!item.physicalBookingDeadline || now <= new Date(item.physicalBookingDeadline).getTime());
  const chosenMode = (item) => item.online && item.physical ? deliveryModes[item.id]
    : item.online ? 'Online' : physicalAvailable(item) ? 'Physical' : null;
  const choices = classSessions.map((item) => ({ courseSessionId: item.id, mode: chosenMode(item) }));
  const choicesReady = !hasSessionChoices || choices.every((choice) => choice.mode
    && (choice.mode !== 'Physical' || physicalAvailable(classSessions.find((item) => item.id === choice.courseSessionId))));
  const afterBalance = state.credits - course.credits;
  const completedEnrollment = (state.enrolled || []).find((item) => item.id === course.id && item.status === 'completed');
  const noPublishedSession = 'This course has no published session yet. A Trainer must open an Intake and the Creator must confirm it.';
  const unavailable = classSessions.map((item) => sessionAvailability(item, now)).find((item) => !item.canReserve);
  const availability = unavailable || sessionAvailability(session, now);
  const openCount = sessions.filter((item) => sessionAvailability(item, now).canReserve).length;

  function tryEnrol() {
    if (course.alreadyEnrolled) {
      showToast('Already enrolled');
      return;
    }
    if (!session) {
      showToast(noPublishedSession);
      return;
    }
    const current = currentAvailability(session);
    if (!current.canReserve) { showToast(current.label); return; }
    if (state.credits < course.credits) {
      setInsufficientOpen(true);
      return;
    }
    setEnrolOpen(true);
  }

  async function confirmEnrol() {
    if (enrolBusy) return;
    const current = currentAvailability(session);
    if (!current.canReserve) {
      setEnrolOpen(false);
      showToast(current.label);
      return;
    }
    setEnrolBusy(true);
    try {
      const result = hasSessionChoices ? await enrol(course.id, session.id, choices) : await enrol(course.id, session.id);
      const updated = await loadCourseDetail(course.id, { fresh: true });
      setEnrolOpen(false);
      setSuccessMsg(`${formatCount(result.creditsSpent, 'credit')} on hold · ${result.balance} available · ${result.heldAfter} on hold in total`);
      setSuccessOpen(true);
      setCourse(updated);
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
        <div className="cx-detail">
          <nav className="cx-crumb" aria-label="Breadcrumb">
            <Link to="/member/courses">Program Catalog</Link>
            <span aria-hidden="true">›</span>
            <span>{course.code}</span>
          </nav>
          <div className="cx-detail-split">
            <div className="cx-detail-main">
              <section className="card cx-detail-hero">
                <CourseArt code={course.code} path={course.learningPath} large />
                <div className="cx-detail-hero-body">
                  <div className="cx-detail-chips">
                    <span className="cx-chip">{course.level || 'Level not specified'}</span>
                    {course.interests?.map((tag) => <span className="pill neutral" key={tag.id}>{tag.name}</span>)}
                  </div>
                  <h2 className="cx-detail-h">About this course</h2>
                  <p className="cx-detail-text">{course.description || 'A course description has not been provided yet.'}</p>
                  <div className="cx-team" aria-label="Course team">
                    <span className="cx-person">
                      <span className="cx-person-avatar" aria-hidden="true">{initials(course.creatorName)}</span>
                      <span><small>Creator</small><b>{course.creatorName || 'Creator not specified'}</b></span>
                    </span>
                    {course.trainerNames?.length ? course.trainerNames.map((name) => (
                      <span className="cx-person" key={name}>
                        <span className="cx-person-avatar is-trainer" aria-hidden="true">{initials(name)}</span>
                        <span><small>Trainer</small><b>{name}</b></span>
                      </span>
                    )) : (
                      <span className="cx-person">
                        <span className="cx-person-avatar is-empty" aria-hidden="true">?</span>
                        <span><small>Trainer</small><span className="cx-muted">A Trainer is assigned when a class opens.</span></span>
                      </span>
                    )}
                  </div>
                </div>
              </section>

              <dl className="cx-kv">
                <div><dt>Course fee</dt><dd className="cx-kv-fee">{formatCount(course.credits, 'credit')}</dd></div>
                <div><dt>Classes listed</dt><dd>{sessions.length || 'None yet'}</dd></div>
                <div><dt>Open for booking</dt><dd>{openCount || 'None'}</dd></div>
                <div><dt>Rating</dt><dd>{course.ratingCount ? `${Number(course.averageStars).toFixed(1)} ★ from ${course.ratingCount} rating(s)` : 'New · No ratings yet'}</dd></div>
              </dl>

              <section className="cx-detail-section" aria-labelledby="cx-classes">
                <div className="cx-sec-h">
                  <h2 className="cx-detail-h" id="cx-classes">Training Sessions</h2>
                  <span className="cx-muted">Choose a class to reserve. You reserve the whole class.</span>
                </div>
                {sessions.length === 0 ? (
                  <div className="card cx-empty-class"><p>{noPublishedSession}</p></div>
                ) : sessions.map((s, i) => {
                  const sAvailability = sessionAvailability(s, now);
                  return (
                    <button
                      type="button"
                      key={s.id}
                      className={`cx-class${i === sessionIdx ? ' selected' : ''}${sAvailability.canReserve ? '' : ' is-closed'}`}
                      aria-pressed={i === sessionIdx}
                      onClick={() => setSessionIdx(i)}
                    >
                      <span className="cx-class-radio" aria-hidden="true" />
                      <span className="cx-class-main">
                        <span className="cx-class-title"><strong>{s.label}</strong><span className={`cx-class-status${sAvailability.canReserve ? ' is-open' : ''}`}>{sAvailability.label}</span></span>
                        <span className="cx-class-when">{s.startsAt && s.endsAt ? formatUtcRange(s.startsAt, s.endsAt) : 'Schedule unavailable'}</span>
                        <span className="cx-class-note">Delivery: {s.physical && s.online ? 'Physical and online (shared capacity)' : s.physical ? 'Physical' : 'Online'}</span>
                        {s.registrationClosesAt ? <span className="cx-class-note">Registration closes {formatUtcDate(s.registrationClosesAt)}</span> : null}
                      </span>
                      <span className="cx-class-side">
                        <span className="cx-class-seats">{(s.capacity ?? 0) > 0 ? `${formatCount(s.seats, 'seat')} left` : 'Online'}</span>
                        <span className="cx-class-note">{formatCount(s.intakeEnrollmentCount ?? 0, 'learner')} reserved or enrolled · Minimum {s.minEnrollment ?? 10}</span>
                      </span>
                    </button>
                  );
                })}
              </section>

              <section className="card">
                <div className="card-header">Learning Outcomes</div>
                <div className="card-body">
                  {(course.outcomes || []).length ? (
                    <ul className="cx-outcomes">
                      {course.outcomes.map((o) => <li key={o}>{o}</li>)}
                    </ul>
                  ) : <p className="cx-muted">Learning outcomes have not been provided yet.</p>}
                </div>
              </section>

              <section className="card">
                <div className="card-header">Certificate requirements</div>
                <div className="card-body cx-muted">Certificate applications require 100% completion, at least 80% attendance and all assessments passed, followed by Trainer and Admin approval.</div>
              </section>
            </div>

            <aside className="cx-detail-aside" aria-label="Reserve a place">
              <section className="card cx-buy">
                <p className="cx-buy-price"><strong>{course.credits}</strong> credits <span>held until the class is confirmed</span></p>
                {session ? (
                  <div className="cx-buy-class">
                    <small>Selected class</small>
                    <b>{session.label}</b>
                    <span>{session.startsAt && session.endsAt ? formatUtcRange(session.startsAt, session.endsAt) : 'Schedule unavailable'}</span>
                  </div>
                ) : null}
                {course.alreadyEnrolled ? null : (
                  <div className="cx-buy-lines">
                    <div><span>Available now</span><b className="is-teal">{state.credits}</b></div>
                    <div><span>After this hold</span><b className={afterBalance < 0 ? 'is-danger' : ''}>{afterBalance}</b></div>
                  </div>
                )}
                {course.alreadyEnrolled ? (
                  <span className="pill success cx-buy-enrolled">Already Enrolled</span>
                ) : (
                  <button type="button" className="btn btn-primary btn-block cx-buy-cta" onClick={tryEnrol} disabled={!availability.canReserve}>{availability.canReserve ? 'Reserve place' : availability.label}</button>
                )}
                {course.suggestedPrerequisite ? <p className="callout">Suggested first: {course.suggestedPrerequisite}. You can still reserve a place in this course.</p> : null}
                <button type="button" className={`btn btn-ghost btn-block cx-buy-wish${state.wishlist.includes(course.id) ? ' is-saved' : ''}`} onClick={() => toggleWish(course.id)}>
                  {state.wishlist.includes(course.id) ? '♥ Saved to Wishlist' : '+ Add to Wishlist'}
                </button>
                {completedEnrollment ? <button type="button" className="btn btn-ghost btn-block" onClick={() => setRatingOpen(true)}>Rate this course</button> : null}
                <p className="cx-buy-note">Credits are held until the class is confirmed at registration close. If it does not run, or you cancel before confirmation, they return to your available balance.</p>
              </section>
            </aside>
          </div>
        </div>
      </MemberShell>

      <Modal open={enrolOpen} title="Confirm Enrolment" onClose={() => { if (!enrolBusy) setEnrolOpen(false); }}>
        <div className="card purple-bg" style={{ marginBottom: 12 }}>
          <div className="card-body">
            <strong>{course.code} — {course.title}</strong>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 6 }}>
              {session ? formatUtcRange(session.startsAt, session.endsAt) : ''} · <span style={{ color: 'var(--purple)', fontWeight: 600 }}>{formatCount(course.credits, 'credit')}</span>
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
        {hasSessionChoices ? <div>
          <h3>Choose how to attend each session</h3>
          {classSessions.map((item) => <fieldset className="form-group" key={item.id}>
            <legend>{item.label}</legend>
            {item.online ? <label><input type="radio" name={`delivery-${item.id}`} aria-label={`Online for ${item.label}`}
              checked={chosenMode(item) === 'Online'} onChange={() => setDeliveryModes((current) => ({ ...current, [item.id]: 'Online' }))} /> Online</label> : null}
            {item.physical ? <label><input type="radio" name={`delivery-${item.id}`} aria-label={`Physical for ${item.label}`}
              checked={chosenMode(item) === 'Physical'} disabled={!physicalAvailable(item)}
              onChange={() => setDeliveryModes((current) => ({ ...current, [item.id]: 'Physical' }))} /> Physical{!physicalAvailable(item) ? ' (unavailable)' : ''}</label> : null}
          </fieldset>)}
        </div> : null}
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" disabled={enrolBusy} onClick={() => setEnrolOpen(false)}>Cancel</button>
          <button type="button" className="btn btn-primary" disabled={enrolBusy || !availability.canReserve || !choicesReady} onClick={confirmEnrol}>{enrolBusy ? 'Reserving…' : 'Confirm Enrolment'}</button>
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
              const back = { returnTo: location.pathname, need: Math.max(0, course.credits - state.credits),
                courseTitle: course.title, sessionId: session.id, userId: state.user?.id };
              saveReservationReturn(back);
              navigate('/member/payment', { state: back });
            }}
          >
            {course.credits > state.credits ? `Top up ${course.credits - state.credits} credits` : 'Top up credits'}
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
