import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import { ApiError } from '../../api/client';
import { useMemberData } from './MemberDataContext';

// Course detail + enrol.
export default function MemberCourseDetailPage() {
  const { courseId } = useParams();
  const navigate = useNavigate();
  const { state, showToast, loadCourseDetail, enrol, toggleWish } = useMemberData();
  const [course, setCourse] = useState(null);
  const [sessionIdx, setSessionIdx] = useState(0);
  const [enrolOpen, setEnrolOpen] = useState(false);
  const [insufficientOpen, setInsufficientOpen] = useState(false);
  const [successOpen, setSuccessOpen] = useState(false);
  const [successMsg, setSuccessMsg] = useState('');

  useEffect(() => {
    let cancelled = false;
    loadCourseDetail(Number(courseId))
      .then((c) => {
        if (!cancelled) setCourse(c);
      })
      .catch((e) => showToast(e.message || 'Failed to load course'));
    return () => {
      cancelled = true;
    };
  }, [courseId, loadCourseDetail, showToast]);

  if (!course) {
    return (
      <MemberShell title="Course" subtitle="Loading…" onNotify={() => {}}>
        <p className="page-sub">Loading course detail…</p>
      </MemberShell>
    );
  }

  const session = course.sessions[sessionIdx] || course.sessions[0];
  const afterBalance = state.credits - course.credits;

  function tryEnrol() {
    if (course.alreadyEnrolled) {
      showToast('Already enrolled');
      return;
    }
    if (state.credits < course.credits) {
      setInsufficientOpen(true);
      return;
    }
    setEnrolOpen(true);
  }

  async function confirmEnrol() {
    try {
      const result = await enrol(course.id, session.id);
      setEnrolOpen(false);
      setSuccessMsg(`${result.creditsSpent} credits deducted · Balance now ${result.balance}`);
      setSuccessOpen(true);
      setCourse((c) => ({ ...c, alreadyEnrolled: true }));
    } catch (e) {
      setEnrolOpen(false);
      if (e instanceof ApiError && e.code === 'INSUFFICIENT_CREDITS') setInsufficientOpen(true);
      else showToast(e.message || 'Enrol failed');
    }
  }

  return (
    <>
      <MemberShell
        title={`${course.code} — ${course.title}`}
        onNotify={() => showToast('No new notifications')}
      >
        <div className="grid-2-1">
          <div>
            <div style={{ height: 160, background: 'var(--purple-soft)', borderRadius: 8, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 32, fontWeight: 700, color: 'var(--purple)', marginBottom: 16 }}>
              {course.code}
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Learning Outcomes</div>
              <div className="card-body" style={{ fontSize: 13, lineHeight: 1.8 }}>
                {course.outcomes.map((o) => (
                  <div key={o}>• {o}</div>
                ))}
              </div>
            </div>
            <div className="card">
              <div className="card-header">Training Sessions</div>
              <div className="card-body">
                {course.sessions.map((s, i) => (
                  <div
                    key={s.id}
                    className={`session-option${i === sessionIdx ? ' selected' : ''}${s.seats === 0 ? ' full' : ''}`}
                    onClick={() => (s.seats === 0 ? showToast('Session full') : setSessionIdx(i))}
                  >
                    <strong>{s.label}</strong> · {s.when}
                    <span className="seats">{s.seats === 0 ? 'Full' : `${s.seats} seats left`}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
          <div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Trainer Information</div>
              <div className="card-body">
                <strong>{course.trainer}</strong>
                <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 6 }}>Senior Trainer</div>
              </div>
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Course Fee</div>
              <div className="card-body">
                <strong style={{ color: 'var(--purple)', fontSize: 18 }}>{course.credits} Credits</strong>
                <span className="pill success" style={{ marginLeft: 8 }}>Certificate included</span>
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
            <button type="button" className="btn btn-primary" onClick={tryEnrol}>Enrol Now</button>
          )}
        </div>
      </MemberShell>

      <Modal open={enrolOpen} title="Confirm Enrolment" onClose={() => setEnrolOpen(false)}>
        <div className="card purple-bg" style={{ marginBottom: 12 }}>
          <div className="card-body">
            <strong>{course.code} — {course.title}</strong>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 6 }}>
              {session?.when} · <span style={{ color: 'var(--purple)', fontWeight: 600 }}>{course.credits} Credits</span>
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
          <span style={{ color: 'var(--slate)' }}>Current balance</span>
          <strong style={{ color: 'var(--teal)' }}>{state.credits}</strong>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 12 }}>
          <span style={{ color: 'var(--slate)' }}>After enrolment</span>
          <strong>{afterBalance}</strong>
        </div>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={() => setEnrolOpen(false)}>Cancel</button>
          <button type="button" className="btn btn-primary" onClick={confirmEnrol}>Confirm Enrolment</button>
        </div>
      </Modal>

      <Modal open={insufficientOpen} title="Insufficient Credits" onClose={() => setInsufficientOpen(false)}>
        <div className="callout warn">
          <div className="callout-title">Cannot enrol</div>
          You need more credits to enrol in this program.
        </div>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={() => setInsufficientOpen(false)}>Cancel</button>
          <button type="button" className="btn btn-teal" onClick={() => { setInsufficientOpen(false); navigate('/member/payment'); }}>Top Up Credits</button>
        </div>
      </Modal>

      <Modal open={successOpen} title="Enrolment Successful" onClose={() => setSuccessOpen(false)}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 48, color: 'var(--teal)' }}>✓</div>
          <strong style={{ display: 'block', margin: '8px 0' }}>You are enrolled!</strong>
          <p style={{ fontSize: 13, color: 'var(--slate)' }}>{successMsg}</p>
          <div className="modal-actions">
            <button type="button" className="btn btn-ghost" onClick={() => { setSuccessOpen(false); navigate('/member/courses'); }}>Browse More</button>
            <button type="button" className="btn btn-primary" onClick={() => { setSuccessOpen(false); navigate('/member/programs'); }}>Go to My Programs</button>
          </div>
        </div>
      </Modal>
    </>
  );
}
