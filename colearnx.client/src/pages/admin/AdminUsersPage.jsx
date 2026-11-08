import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Modal from '../../components/Modal';
import AdminUserDetail from './AdminUserDetail';
import AdminCreditLedgerPage from './AdminCreditLedgerPage';
import { loadAdminUsers } from './adminUserLookup';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

export default function AdminUsersPage() {
  const [params, setParams] = useSearchParams();
  const selectedId = /^[1-9]\d*$/.test(params.get('userId') || '') ? Number(params.get('userId')) : null;
  const showLedger = params.get('view') === 'ledger';
  function selectUser(id) { setParams(id ? { userId: String(id) } : {}); }
  const [search, setSearch] = useState('');
  const [applied, setApplied] = useState('');
  const query = useAdminQuery(loadAdminUsers, applied);
  return <section className="admin-review-page later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Account lookup</p><h1>Users</h1><p>Find accounts by name, email or user ID, including users without ledger entries.</p></div></header>
    <div className="later-actions"><button type="button" className="btn btn-ghost" aria-pressed={!showLedger} onClick={() => setParams({})}>User accounts</button><button type="button" className="btn btn-ghost" aria-pressed={showLedger} onClick={() => setParams({ view: 'ledger' })}>All credit transactions</button></div>
    {showLedger ? <AdminCreditLedgerPage onBalanceChange={query.refresh} /> : <>
    <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied(search.trim()); }}>
      <label>Find a user<input value={search} maxLength={254} onChange={(event) => setSearch(event.target.value)} placeholder="Name, email or user ID" /></label>
      <button className="btn btn-primary">Search users</button>
    </form>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    {query.data ? <><p>Showing up to 100 matching accounts. Refine the search to find a specific user.</p><div className="later-table-wrap"><table className="later-table admin-users-table"><thead><tr><th>User</th><th>Roles</th><th>Available credits</th><th>On hold</th><th>Status</th><th>Action</th></tr></thead><tbody>
      {query.data.map((user) => <tr key={user.id} className="account-click-row" onClick={() => selectUser(user.id)}><td><button type="button" className="account-name-button" onClick={(event) => { event.stopPropagation(); selectUser(user.id); }}>{user.fullName}</button><small>{user.email} · #{user.id}</small></td><td>{user.roles.length ? <span className="admin-role-pills">{user.roles.map((role) => <span key={role} className="pill neutral">{role}</span>)}</span> : 'No roles'}</td><td>{user.creditBalance}</td><td>{user.heldCredits}</td><td><span className={`pill ${user.isActive ? 'success' : 'neutral'}`}>{user.isActive ? 'Active' : 'Inactive'}</span></td><td><button type="button" className="btn btn-ghost btn-sm" onClick={(event) => { event.stopPropagation(); selectUser(user.id); }}>View details</button></td></tr>)}
    </tbody></table>{!query.data.length ? <div className="admin-review-state">No users match your search.</div> : null}</div></> : null}
    </>}
    <Modal open={selectedId !== null} title="User details" onClose={() => selectUser(null)} width={1200}>{selectedId !== null ? <AdminUserDetail key={selectedId} userId={selectedId} onBalanceChange={query.refresh} /> : null}</Modal>
  </section>;
}
