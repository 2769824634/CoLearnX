import useStaffAuth from '../../auth/useStaffAuth';
import StaffDetails from '../../components/StaffDetails';
export default function StaffAccountPage() {
  const { token } = useStaffAuth();
  return <section className="creator-page staff-account"><header className="creator-header"><div><p className="creator-eyebrow">Platform staff</p><h1>My account</h1><p>Your employee information, document and teaching assignments.</p></div></header><StaffDetails key={token} token={token} /></section>;
}
