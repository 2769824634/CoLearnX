import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '../auth/RequireAuth';
import RoleShell, { RolePlaceholder } from '../layouts/RoleShell';
import LoginPage from '../pages/LoginPage';
import { MemberDataProvider, useMemberData } from '../pages/member/MemberDataContext';
import MemberHomePage from '../pages/member/MemberHomePage';
import MemberCatalogPage from '../pages/member/MemberCatalogPage';
import MemberCourseDetailPage from '../pages/member/MemberCourseDetailPage';
import MemberProgramsPage from '../pages/member/MemberProgramsPage';
import MemberPaymentPage from '../pages/member/MemberPaymentPage';
import MemberBadgesPage from '../pages/member/MemberBadgesPage';
import MemberAccountPage from '../pages/member/MemberAccountPage';

function MemberToastHost() {
  const { toast } = useMemberData();
  return <div className={`toast${toast ? ' show' : ''}`}>{toast}</div>;
}

// Member routes under /member/*. Keep page component names below.
function MemberArea() {
  return (
    <MemberDataProvider>
      <Routes>
        <Route path="home" element={<MemberHomePage />} />
        <Route path="courses" element={<MemberCatalogPage />} />
        <Route path="courses/:courseId" element={<MemberCourseDetailPage />} />
        <Route path="programs" element={<MemberProgramsPage />} />
        <Route path="payment" element={<MemberPaymentPage />} />
        <Route path="badges" element={<MemberBadgesPage />} />
        <Route path="account" element={<MemberAccountPage />} />
        <Route path="*" element={<Navigate to="home" replace />} />
      </Routes>
      <MemberToastHost />
    </MemberDataProvider>
  );
}

function stub(title, body) {
  return <RolePlaceholder heading={title} body={body} />;
}

// Top-level router. Shared: AppRouter
export default function AppRouter() {
  return (
    <div className="app-wrap">
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<Navigate to="/login" replace />} />

        <Route element={<RequireAuth role="member" />}>
          <Route path="/member/*" element={<MemberArea />} />
        </Route>

        <Route element={<RequireAuth role="trainer" />}>
          <Route path="/trainer" element={<RoleShell role="trainer" />}>
            <Route path="home" element={stub('Trainer Dashboard (TRN-01)', 'Framework shell ready — KPI, today sessions, todos.')} />
            <Route path="courses" element={stub('Courses (TRN-02)', 'Create wizard + content library placeholders.')} />
            <Route path="attendance" element={stub('Attendance (TRN-05)', 'Session roster Present/Absent/Late.')} />
            <Route path="learners" element={stub('Learner List (TRN-04)', 'Cohort roster + bulk message.')} />
            <Route path="account" element={stub('My Account', 'Read-only masked profile + Edit Profile modal (shared pattern).')} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAuth role="creator" />}>
          <Route path="/creator" element={<RoleShell role="creator" />}>
            <Route path="home" element={stub('Creator Home (CRT-01)', 'Materials / adoption / royalty overview.')} />
            <Route path="courses" element={stub('Courses', 'Create course from approved materials.')} />
            <Route path="upload" element={stub('Upload Material (CRT-01)', 'Three-step upload wizard → Admin review.')} />
            <Route path="usage" element={stub('Usage Records (CRT-03)', 'Adoption + royalty analytics.')} />
            <Route path="account" element={stub('My Account', 'Masked account + Edit Profile modal.')} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAuth role="admin" />}>
          <Route path="/admin" element={<RoleShell role="admin" />}>
            <Route path="home" element={stub('Admin Homepage (ADM-01)', 'Pending KPI + SLA queues.')} />
            <Route path="approvals" element={stub('Approvals (ADM-02)', 'Materials / Courses / Certificates tabs.')} />
            <Route path="users" element={stub('Users & Roles (ADM-03)', 'RoleRequest review + suspend/assign.')} />
            <Route path="ledger" element={stub('Credit Ledger (ADM-04)', 'Wired to GET /api/admin/credits/ledger.')} />
            <Route path="disputes" element={stub('Disputes & Refunds (ADM-05)', 'Open case → refund / reject.')} />
            <Route path="audit" element={stub('Audit Log', 'Immutable Admin actions (BR-10).')} />
            <Route path="account" element={stub('My Account', 'Masked account + Edit Profile modal.')} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </div>
  );
}
