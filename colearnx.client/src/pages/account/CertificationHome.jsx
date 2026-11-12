import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { roleRequestsApi } from '../../api';
import '../../styles/certification.css';

export default function CertificationHome({ role }) {
  const [latest, setLatest] = useState(undefined);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const rolePath = role.toLowerCase();

  useEffect(() => {
    let cancelled = false;
    roleRequestsApi.my().then(
      (items) => {
        if (cancelled) return;
        if (!Array.isArray(items)) { setError(true); return; }
        setLatest(items.find((item) => item.requestedRole === role) || null);
      },
      () => { if (!cancelled) setError(true); },
    );
    return () => { cancelled = true; };
  }, [role, attempt]);

  const status = latest === undefined ? 'Checking your application…'
    : !latest ? 'You have not applied for certification yet.'
      : latest.status === 'Pending' ? 'Submitted. Waiting for administrator review.'
        : latest.status === 'Rejected' ? `Not approved${latest.reviewNote ? `: ${latest.reviewNote}` : '.'} You can submit again.`
          : 'Approved. Check your approval to open your workspace.';
  const action = latest?.status === 'Pending' ? 'View my application'
    : latest?.status === 'Approved' ? 'Check approval' : 'Start certification';

  return (
    <section className={`${rolePath}-page`}>
      <header className={`${rolePath}-header`}>
        <div>
          <p className={`${rolePath}-eyebrow`}>{role} workspace</p>
          <h1>Welcome to your {role} workspace</h1>
          <p>Complete certification to use your workspace features. You can edit your profile while you wait.</p>
        </div>
      </header>
      <div className="card certification-panel">
        <h2>Certification status</h2>
        {error ? (
          <div role="alert">
            <p>We could not load your certification status.</p>
            <button type="button" className="btn btn-ghost" onClick={() => { setError(false); setLatest(undefined); setAttempt((value) => value + 1); }}>Retry</button>
          </div>
        ) : <p role="status"><strong>Status: </strong><span>{status}</span></p>}
        <div className="certification-actions">
          <Link className={latest?.status === 'Pending' ? 'btn btn-ghost' : 'btn btn-primary'} to={`/${rolePath}/certification`}>{action}</Link>
          <Link className="btn btn-ghost" to={`/${rolePath}/account`}>Edit my profile</Link>
        </div>
      </div>
    </section>
  );
}
