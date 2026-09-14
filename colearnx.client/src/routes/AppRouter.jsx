import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '../auth/RequireAuth';
import RequireAdmin from '../auth/RequireAdmin';
import RoleShell, { AdminRoleShell, RolePlaceholder } from '../layouts/RoleShell';
import LoginPage from '../pages/LoginPage';
import AdminLoginPage from '../pages/admin/AdminLoginPage';
import AdminHomePage from '../pages/admin/AdminHomePage';
import AdminApprovalsPage from '../pages/admin/AdminApprovalsPage';
import AdminAuditLogsPage from '../pages/admin/AdminAuditLogsPage';
import AdminAccountPage from '../pages/admin/AdminAccountPage';
import AdminCreditLedgerPage from '../pages/admin/AdminCreditLedgerPage';
import AdminDisputesPage from '../pages/admin/AdminDisputesPage';
import TrainerHomePage from '../pages/trainer/TrainerHomePage';
import TrainerIntakesPage from '../pages/trainer/TrainerIntakesPage';
import TrainerIntakeCreatePage from '../pages/trainer/TrainerIntakeCreatePage';
import TrainerIntakeDetailPage from '../pages/trainer/TrainerIntakeDetailPage';
import TrainerAttendancePage from '../pages/trainer/TrainerAttendancePage';
import TrainerLearnersPage from '../pages/trainer/TrainerLearnersPage';
import CreatorHomePage from '../pages/creator/CreatorHomePage';
import CreatorCoursesPage from '../pages/creator/CreatorCoursesPage';
import CreatorCourseFormPage from '../pages/creator/CreatorCourseFormPage';
import CreatorIntakeApplicationsPage from '../pages/creator/CreatorIntakeApplicationsPage';
import CreatorIntakeApplicationDetailPage from '../pages/creator/CreatorIntakeApplicationDetailPage';
import { MemberDataProvider } from '../pages/member/MemberDataContext';
import { useMemberData } from '../pages/member/memberDataState';
import MemberHomePage from '../pages/member/MemberHomePage';
import MemberCatalogPage from '../pages/member/MemberCatalogPage';
import MemberCourseDetailPage from '../pages/member/MemberCourseDetailPage';
import MemberProgramsPage from '../pages/member/MemberProgramsPage';
import MemberPaymentPage from '../pages/member/MemberPaymentPage';
import MemberBadgesPage from '../pages/member/MemberBadgesPage';
import MemberAccountPage from '../pages/member/MemberAccountPage';
import CreatorUploadPage from '../pages/creator/CreatorUploadPage';

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
        <Route path="/admin/login" element={<AdminLoginPage />} />
        <Route path="/" element={<Navigate to="/login" replace />} />

        <Route element={<RequireAuth role="member" />}>
          <Route path="/member/*" element={<MemberArea />} />
        </Route>

        <Route element={<RequireAuth role="trainer" />}>
          <Route path="/trainer" element={<RoleShell role="trainer" />}>
            <Route path="home" element={<TrainerHomePage />} />
            <Route path="courses" element={<TrainerIntakesPage />} />
            <Route path="courses/new" element={<TrainerIntakeCreatePage />} />
            <Route path="courses/intakes/:courseIntakeId" element={<TrainerIntakeDetailPage />} />
            <Route path="attendance" element={<TrainerAttendancePage />} />
            <Route path="learners" element={<TrainerLearnersPage />} />
            <Route path="account" element={stub('My Account', 'Read-only masked profile + Edit Profile modal (shared pattern).')} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAuth role="creator" />}>
          <Route path="/creator" element={<RoleShell role="creator" />}>
            <Route path="home" element={<CreatorHomePage />} />
            <Route path="courses" element={<CreatorCoursesPage />} />
            <Route path="courses/new" element={<CreatorCourseFormPage />} />
            <Route path="courses/:courseId" element={<CreatorCourseFormPage />} />
            <Route path="courses/intake-applications" element={<CreatorIntakeApplicationsPage />} />
            <Route path="courses/intake-applications/:courseIntakeId" element={<CreatorIntakeApplicationDetailPage />} />
            <Route path="upload" element={<CreatorUploadPage />} />
            <Route path="usage" element={stub('Usage Records (CRT-03)', 'Adoption + royalty analytics.')} />
            <Route path="account" element={stub('My Account', 'Masked account + Edit Profile modal.')} />
            <Route index element={<Navigate to="home" replace />} />
          </Route>
        </Route>

        <Route element={<RequireAdmin />}>
          <Route path="/admin" element={<AdminRoleShell />}>
            <Route path="home" element={<AdminHomePage />} />
            <Route path="approvals" element={<AdminApprovalsPage />} />
            <Route path="users" element={<Navigate to="/admin/approvals?queue=roles" replace />} />
            <Route path="ledger" element={<AdminCreditLedgerPage />} />
            <Route path="disputes" element={<AdminDisputesPage />} />
            <Route path="audit" element={<AdminAuditLogsPage />} />
            <Route path="account" element={<AdminAccountPage />} />
            <Route index element={<Navigate to="home" replace />} />
            <Route path="*" element={<Navigate to="/admin/home" replace />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </div>
  );
}
