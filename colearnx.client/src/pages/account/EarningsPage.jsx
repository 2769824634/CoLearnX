import { earningsApi } from '../../api';
import { formatCount, formatUtcDate, userFacingError } from '../businessPresentation';
import useTrainerQuery from '../trainer/useTrainerQuery';

const loadTrainerEarnings = (token, _key, signal) => earningsApi.trainer(token, signal);
const loadCreatorEarnings = (token, _key, signal) => earningsApi.creator(token, signal);

export default function EarningsPage({ role }) {
  const query = useTrainerQuery(role === 'trainer' ? loadTrainerEarnings : loadCreatorEarnings);
  const data = query.data;
  return <section className={`${role}-page`}>
    <h1>Earnings</h1>
    <p>Credits are recorded when a class is confirmed. Payouts are not available yet.</p>
    {query.loading ? <p role="status">Loading earnings…</p> : null}
    {query.error ? <div className="callout warn" role="alert">{userFacingError(query.error, 'Could not load earnings.')}
      <button type="button" className="btn btn-ghost" onClick={query.refresh}>Retry</button></div> : null}
    {data ? <>
      <div className="grid-3">
        <div className="card card-body"><strong>Total earnings</strong><span>{formatCount(data.totalCredits, 'credit')}</span></div>
        <div className="card card-body"><strong>This month</strong><span>{formatCount(data.thisMonthCredits, 'credit')}</span></div>
        <div className="card card-body"><strong>Confirmed classes</strong><span>{data.confirmedClasses}</span></div>
      </div>
      <p>{data.sharePercent == null ? 'Shares follow each course’s price pool.' : `Current share: ${data.sharePercent}% of the class credits.`} Refunds adjust the recorded earnings.</p>
      {!data.rows.length ? <p>No earnings yet. Earnings appear when a class is confirmed to run.</p> :
        <div style={{ overflowX: 'auto' }}><table className="data-table">
          <thead><tr><th>Course</th><th>Class starts</th><th>Learners</th><th>Class credits</th><th>Your share</th></tr></thead>
          <tbody>{data.rows.map((row, index) => <tr key={`${row.intakeId}-${row.createdAt}-${index}`}>
            <td>{row.courseCode} · {row.courseTitle}</td><td>{formatUtcDate(row.startsAt)}</td>
            <td>{row.isReversal ? 'Refund adjustment' : row.learnerCount}</td><td>{row.grossCredits}</td><td>{row.credits}</td>
          </tr>)}</tbody>
        </table></div>}
    </> : null}
  </section>;
}
