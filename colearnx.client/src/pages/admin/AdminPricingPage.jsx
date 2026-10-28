import { adminPricingApi } from '../../api';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

function loadPool(token, _key, signal) {
  return adminPricingApi.pool(token, signal);
}

function band(min, max) {
  return `${min}–${max}`;
}

export default function AdminPricingPage() {
  const pool = useAdminQuery(loadPool, 'pool');

  return (
    <section className="admin-review-page">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Reference data</p>
          <h1>Pricing</h1>
          <p>Company price limits. These nine cells cannot be edited here.</p>
        </div>
      </header>
      <AdminDataState loading={pool.loading} error={pool.error} onRetry={pool.refresh} />
      {pool.data ? (
        <section className="admin-review-workspace" aria-label="Price pool">
          <div className="admin-review-table-wrap">
            <table className="admin-review-table admin-pricing-pool" aria-label="Company price limits">
              <thead>
                <tr>
                  <th scope="col">Subject</th>
                  <th scope="col">Level</th>
                  <th scope="col">Total credits</th>
                  <th scope="col">Live class share</th>
                  <th scope="col">Video course share</th>
                </tr>
              </thead>
              <tbody>
                {pool.data.map((row) => (
                  <tr key={row.id}>
                    <td>{row.subject}</td>
                    <td>{row.levelName}</td>
                    <td className="admin-pricing-figure">{band(row.minTotalCredits, row.maxTotalCredits)}</td>
                    <td className="admin-pricing-share">25 / 25 / 50</td>
                    <td className="admin-pricing-share">50 / 0 / 50</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p>Shares are content, delivery, then platform. Staff are paid a salary and do not receive these shares.</p>
        </section>
      ) : null}
    </section>
  );
}
