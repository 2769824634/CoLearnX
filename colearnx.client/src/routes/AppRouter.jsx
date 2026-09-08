import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '../auth/RequireAuth';
import RoleShell from '../layouts/RoleShell';
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

function PageTitle({ children }) {
  return <h1 className="page-title">{children}</h1>;
}

// Top-level router.
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
            <Route path="home" element={<PageTitle>Trainer Dashboard</PageTitle>} />
            <Route path="courses" element={<PageTitle>Courses</PageTitle>} />
            <Route path="attendance" element={<PageTitle>Attendance</PageTitle>} />
            <Route path="learners" element={<PageTitle>Learner List</PageTitle>} />
            <Route path="account" element={<PageTitle>My Account</PageTitle>} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAuth role="creator" />}>
          <Route path="/creator" element={<RoleShell role="creator" />}>
            <Route path="home" element={<PageTitle>Creator Home</PageTitle>} />
            <Route path="courses" element={<PageTitle>Courses</PageTitle>} />
            <Route path="upload" element={<PageTitle>Upload Material</PageTitle>} />
            <Route path="usage" element={<PageTitle>Usage Records</PageTitle>} />
            <Route path="account" element={<PageTitle>My Account</PageTitle>} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAuth role="admin" />}>
          <Route path="/admin" element={<RoleShell role="admin" />}>
            <Route path="home" element={<PageTitle>Admin Homepage</PageTitle>} />
            <Route path="approvals" element={<PageTitle>Approvals</PageTitle>} />
            <Route path="users" element={<PageTitle>Users & Roles</PageTitle>} />
            <Route path="ledger" element={<PageTitle>Credit Ledger</PageTitle>} />
            <Route path="disputes" element={<PageTitle>Disputes & Refunds</PageTitle>} />
            <Route path="audit" element={<PageTitle>Audit Log</PageTitle>} />
            <Route path="account" element={<PageTitle>My Account</PageTitle>} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </div>
  );
}
