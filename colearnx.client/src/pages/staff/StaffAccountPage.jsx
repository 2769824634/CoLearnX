import useStaffAuth from '../../auth/useStaffAuth';
import StaffDetails from '../../components/StaffDetails';
export default function StaffAccountPage() {
  const { token } = useStaffAuth();
  return <section className="trainer-page"><header className="page-header"><div><p className="eyebrow">Platform staff</p><h1>My account</h1><p>Your employee information, document and teaching assignments.</p></div></header><StaffDetails key={token} token={token} /></section>;
}
