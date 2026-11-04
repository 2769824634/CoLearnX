import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import useCachedQuery from '../../api/useCachedQuery';
import useAdminAuth from '../../auth/useAdminAuth';
import AdminDataState from './AdminDataState';
import AdminCreditLedgerPage from './AdminCreditLedgerPage';
import { formatUtcDateTime } from '../businessPresentation';

const loadUser = (token, id, signal) => adminLaterPhaseApi.user(token, id, signal);
export default function AdminUserDetail({ userId, onBalanceChange }) {
  const { token } = useAdminAuth();
  const query = useCachedQuery(token, loadUser, String(userId));
  const user = query.data;
  function refreshBalances(result) {
    query.setData({ ...user, creditBalance: result.balanceAfter });
    onBalanceChange?.();
  }
  return <div className="account-details">
    {query.error?.status === 404 ? <p role="alert">User was not found.</p> : <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />}
    {user ? <><dl className="account-detail-grid">
      {Object.entries({ 'Full name': user.fullName, 'Display name': user.displayName, 'User ID': user.id, 'Email': user.email, 'Phone': user.phone, 'Country': user.country, 'Occupation': user.occupation, 'Roles': user.roles.join(', '), 'Status': user.isActive ? 'Active' : 'Inactive', 'Email verified': user.emailVerifiedAt ? 'Yes' : 'No', 'Joined (UTC)': formatUtcDateTime(user.createdAt), 'Available credits': user.creditBalance, 'Credits on hold': user.heldCredits, 'Bio': user.bio, 'Learning goals': user.learningGoals, 'Trainer headline': user.trainerHeadline, 'Specialisations': user.specialisations, 'Creator headline': user.creatorHeadline, 'Expertise': user.expertiseTags }).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? '—'}</dd></div>)}
    </dl></> : null}
    {user && !query.error ? <AdminCreditLedgerPage key={userId} userId={userId} onBalanceChange={refreshBalances} /> : null}
  </div>;
}
