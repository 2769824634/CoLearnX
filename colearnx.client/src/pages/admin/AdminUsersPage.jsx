import { useState } from 'react';
import { Link } from 'react-router-dom';
import { loadAdminUsers } from './adminUserLookup';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

export default function AdminUsersPage() {
  const [search, setSearch] = useState('');
  const [applied, setApplied] = useState('');
  const query = useAdminQuery(loadAdminUsers, applied);
  return <section className="admin-review-page later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Account lookup</p><h1>Users</h1><p>Find accounts by name, email or user ID, including users without ledger entries.</p></div></header>
    <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied(search.trim()); }}>
      <label>Find a user<input value={search} maxLength={254} onChange={(event) => setSearch(event.target.value)} placeholder="Name, email or user ID" /></label>
      <button className="btn btn-primary">Search users</button>
    </form>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    {query.data ? <><p>Showing up to 100 matching accounts. Refine the search to find a specific user.</p><div className="later-table-wrap"><table className="later-table"><thead><tr><th>User</th><th>Roles</th><th>Available credits</th><th>On hold</th><th>Status</th><th>Action</th></tr></thead><tbody>
      {query.data.map((user) => <tr key={user.id}><td><strong>{user.fullName}</strong><small>{user.email} · #{user.id}</small></td><td>{user.roles.join(', ') || 'No roles'}</td><td>{user.creditBalance}</td><td>{user.heldCredits}</td><td>{user.isActive ? 'Active' : 'Inactive'}</td><td><Link to={`/admin/ledger?userId=${user.id}`}>Review credits</Link></td></tr>)}
    </tbody></table>{!query.data.length ? <div className="admin-review-state">No users match your search.</div> : null}</div></> : null}
  </section>;
}
