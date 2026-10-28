import { useEffect, useState } from 'react';
import { adminStaffApi } from '../../api';
import useAdminAuth from '../../auth/useAdminAuth';

export default function AdminStaffPage() {
  const { token } = useAdminAuth();
  const [staff, setStaff] = useState([]);
  const [form, setForm] = useState({ email: '', fullName: '', phone: '', document: null });
  const [initialPassword, setInitialPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    adminStaffApi.list(token, controller.signal).then(setStaff)
      .catch((err) => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [token]);

  async function provision(event) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setInitialPassword('');
    try {
      const result = await adminStaffApi.provision(token, form);
      setStaff((items) => [...items, result.staff].sort((a, b) => a.staffNumber - b.staffNumber));
      setInitialPassword(result.initialPassword);
      setForm({ email: '', fullName: '', phone: '', document: null });
      event.currentTarget.reset();
    } catch (err) {
      setError(err.message || 'Could not provision staff.');
    } finally {
      setBusy(false);
    }
  }

  async function toggle(account) {
    try {
      const updated = await adminStaffApi.setActive(token, account.id, !account.isActive);
      setStaff((items) => items.map((item) => item.id === updated.id ? updated : item));
    } catch (err) {
      setError(err.message || 'Could not update staff status.');
    }
  }

  return <section className="admin-page">
    <div className="page-header"><div><p className="eyebrow">Platform operations</p><h1>Staff management</h1><p>Provision independent staff sign-ins and maintain their access.</p></div></div>
    {error ? <div className="callout warn" role="alert">{error}</div> : null}
    {initialPassword ? <div className="callout success" role="status"><strong>Initial password — copy it now:</strong> <code>{initialPassword}</code></div> : null}
    <form className="card admin-staff-form" onSubmit={provision}>
      <div className="form-group"><label htmlFor="staff-email">Email</label><input id="staff-email" type="email" required value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} /></div>
      <div className="form-group"><label htmlFor="staff-name">Full name</label><input id="staff-name" required value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} /></div>
      <div className="form-group"><label htmlFor="staff-phone">Phone</label><input id="staff-phone" value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} /></div>
      <div className="form-group"><label htmlFor="staff-document">Staff document</label><input id="staff-document" type="file" accept=".pdf,.png,.jpg,.jpeg" required onChange={(event) => setForm({ ...form, document: event.target.files?.[0] || null })} /></div>
      <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? 'Provisioning…' : 'Provision staff account'}</button>
    </form>
    <div className="table-wrap"><table><thead><tr><th>Number</th><th>Public name</th><th>Employee</th><th>Email</th><th>Status</th><th>Action</th></tr></thead>
      <tbody>{staff.map((account) => <tr key={account.id}><td>{String(account.staffNumber).padStart(3, '0')}</td><td>{account.displayName}</td><td>{account.fullName}</td><td>{account.email}</td><td>{account.isActive ? 'Active' : 'Inactive'}</td><td><button type="button" className="btn btn-ghost btn-sm" onClick={() => toggle(account)}>{account.isActive ? 'Deactivate' : 'Activate'}</button></td></tr>)}</tbody>
    </table></div>
  </section>;
}
