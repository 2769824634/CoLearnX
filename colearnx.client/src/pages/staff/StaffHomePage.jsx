import { Link } from 'react-router-dom';
import useStaffAuth from '../../auth/useStaffAuth';

export default function StaffHomePage() {
  const { staff } = useStaffAuth();
  return <section className="trainer-page"><div className="page-header"><div><p className="eyebrow">Platform staff</p><h1>{staff?.displayName}</h1><p>Create and operate courses published under CoLearnX.</p></div></div>
    <div className="grid-3"><Link className="card" to="/staff/courses"><h2>Platform courses</h2><p>Create drafts and submit them for Admin approval.</p></Link></div>
  </section>;
}
