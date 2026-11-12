import { useState } from 'react';
import { earningsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import { formatCount, userFacingError } from '../businessPresentation';
import { localDateTime } from '../creator/openClass';
import useTrainerQuery from '../trainer/useTrainerQuery';

const loadEarnings = (token, _key, signal) => earningsApi.my(token, signal);

export default function EarningsPage({ role }) {
  const { token } = useAuth();
  const query = useTrainerQuery(loadEarnings);
  const [method, setMethod] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState(null);
  const data = query.data;
  const requested = data?.payouts?.some((item) => item.status === 'Requested');
  const canRequest = data && data.availableCredits >= data.minimumPayoutCredits && !requested && method.trim();
  const requestPayout = async (event) => {
    event.preventDefault(); setBusy(true); setError(null); setNotice('');
    try {
      await earningsApi.requestPayout(method.trim(), token);
      setMethod(''); setNotice('Payout request submitted for administrator review.'); query.refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  };
  return <section className={`${role}-page earnings-page`}>
    <header className="earnings-header">
      <h1>Earnings</h1>
      <p>Earnings are recorded when an intake finishes, then remain pending for 7 days before they can be requested.</p>
    </header>
    {query.loading ? <p role="status">Loading earnings…</p> : null}
    {query.error ? <div className="callout warn earnings-alert" role="alert"><span>{userFacingError(query.error, 'Could not load earnings.')}</span>
      <button type="button" className="btn btn-ghost" onClick={query.refresh}>Retry</button></div> : null}
    {data ? <>
      <div className="earnings-summary">
        <div className="earnings-stats">
          <div className="stat-box"><div className="label">Total earned</div><div className="value">{formatCount(data.totalCredits, 'credit')}</div></div>
          <div className="stat-box"><div className="label">Available to pay out</div><div className="value">{formatCount(data.availableCredits, 'credit')}</div>
            <div className="earnings-stat-note">≈ ${(data.availableCredits * data.cashPerCredit).toFixed(2)}</div></div>
          <div className="stat-box"><div className="label">Paid out</div><div className="value">{formatCount(data.paidCredits, 'credit')}</div></div>
        </div>
        <p className="earnings-note">{formatCount(data.pendingCredits, 'credit')} become available 7 days after each intake ends.</p>
      </div>
      <form className="card card-body earnings-payout" onSubmit={requestPayout}>
        <h2>Request payout</h2>
        {requested ? <p className="callout info" role="status">Payout of {formatCount(data.payouts.find((item) => item.status === 'Requested').credits, 'credit')} requested on {localDateTime(data.payouts.find((item) => item.status === 'Requested').requestedAt)}. An administrator pays it within a few working days.</p> : <>
          <div className="form-group">
            <label htmlFor="earnings-payout-method">Where should we pay you?</label>
            <input id="earnings-payout-method" aria-label="Payout method" maxLength="256" value={method} disabled={busy}
              placeholder="PayPal email or bank details" onChange={(event) => setMethod(event.target.value)} />
          </div>
          <div className="earnings-payout-actions">
            <button className="btn btn-primary" disabled={!canRequest || busy}>{busy ? 'Submitting…' : `Request payout of ${formatCount(data.availableCredits, 'credit')}`}</button>
            {data.availableCredits < data.minimumPayoutCredits ? <p>The minimum payout is {formatCount(data.minimumPayoutCredits, 'credit')}.</p> : null}
          </div>
        </>}
        {notice ? <p className="callout success" role="status">{notice}</p> : null}
        {error ? <p className="callout warn" role="alert">{userFacingError(error, 'Payout request failed.')}</p> : null}
      </form>
      <section className="earnings-section">
        <h2>Earnings by intake</h2>
        {!data.classes.length ? <p className="earnings-empty">No earnings yet. Earnings appear after an intake finishes.</p> :
          <div className="earnings-table"><table className="data-table"><thead><tr><th>Course</th><th>Intake ended</th><th>Learners</th><th>Your share</th><th>Credits</th><th>Status</th></tr></thead>
            <tbody>{data.classes.map((item) => <tr key={item.courseIntakeId}><td>{item.courseCode} · {item.courseTitle}</td>
              <td>{localDateTime(item.endsAt)}</td><td>{item.learners}</td><td>{item.sharePercent == null ? 'Varies' : `${item.sharePercent}%`}</td>
              <td>{formatCount(item.credits, 'credit')}</td><td>{item.status}{item.status === 'Pending' ? ` · available ${localDateTime(item.availableAt)}` : ''}</td></tr>)}</tbody></table></div>}
      </section>
      <section className="earnings-section">
        <h2>Payout history</h2>
        {!data.payouts.length ? <p className="earnings-empty">No payout requests yet.</p> : <div className="earnings-table"><table className="data-table">
          <thead><tr><th>Requested</th><th>Credits</th><th>Method</th><th>Status</th><th>Reference</th></tr></thead>
          <tbody>{data.payouts.map((item) => <tr key={item.id}><td>{localDateTime(item.requestedAt)}</td><td>{item.credits}</td>
            <td>{item.payoutMethod}</td><td>{item.status}</td><td>{item.reference || '—'}</td></tr>)}</tbody></table></div>}
      </section>
    </> : null}
  </section>;
}
