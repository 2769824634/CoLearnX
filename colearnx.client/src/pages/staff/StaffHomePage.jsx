import { Link } from 'react-router-dom';
import useStaffAuth from '../../auth/useStaffAuth';
import { RowArrow } from '../trainer/TrainerUi';

export default function StaffHomePage() {
  const { staff } = useStaffAuth();
  return <section className="creator-page staff-home"><div className="creator-header"><div><p className="creator-eyebrow">Platform staff</p><h1>{staff?.displayName}</h1><p>Create and operate courses published under CoLearnX.</p></div></div>
    <div className="staff-home-cards"><Link className="staff-home-card" to="/staff/account"><span className="creator-eyebrow">My account</span><h2>{staff?.fullName || "My account"}</h2><p className="staff-home-meta">{staff?.email}</p><p>View employee information, uploaded document and teaching assignments.</p><span className="staff-home-go">Open<RowArrow /></span></Link><Link className="staff-home-card" to="/staff/courses"><span className="creator-eyebrow">Catalogue</span><h2>Platform courses</h2><p>Create drafts and submit them for Admin approval.</p><span className="staff-home-go">Open<RowArrow /></span></Link></div>
  </section>;
}
