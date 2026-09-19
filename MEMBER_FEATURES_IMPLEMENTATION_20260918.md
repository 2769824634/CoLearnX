# Member features implementation and local verification

Work directory: `C:\Users\user\Documents\CoLearnX\.local-deploy\CoLearnX-master-20260918`

Baseline: `db8fadd761fab744024431dc98d71e479e829f46` on local `master`.
Changes are local and uncommitted. No push/deployment was performed; the parent
dirty checkout was not edited.

## Implemented behavior

1. **Badges & Certificates**: actual issued certificates, course information,
   stage, award time, verification number; loading/error/retry/empty states.
   The repository has no independent badge award rules. The UI explicitly labels
   certificate stages and does not invent independent earned badges.
2. **Certificate applications**: server-derived eligibility/reasons, guarded
   submission, existing-request handling. Existing completion (100%), attendance
   (Present/Late >=80%) and all-assessments-passed rules are retained.
3. **Progress**: current Member's requests, Submitted/TrainerApproved/
   TrainerRejected/AdminRejected/Issued status, notes and review timestamps.
   Trainer first review and Admin final issuance remain mandatory. Rejected
   requests cannot be resubmitted; replay returns the same record.
4. **Notifications**: one Member-wide bell/inbox; persisted unread count,
   individual/all-read, retry/empty state, controlled internal business links.
   Submission, both approvals and either rejection generate transactional events.
   Concurrent/repeated transitions do not duplicate certificate, audit or notices.
5. **Forgot password**: login link, request page, single-use reset page and SMTP
   integration. See `PASSWORD_RESET_LOCAL.md` for Gmail and database setup.

## Main entry points

| Area | Files |
| --- | --- |
| Certificate backend | `CoLearnX.Server/Services/CertificateWorkflowService.cs`, `Services/AppServices.cs`, `Controllers/ApiControllers.cs` |
| Certificate frontend | `colearnx.client/src/pages/member/MemberBadgesPage.jsx` |
| Notification backend | `CoLearnX.Server/Controllers/NotificationsController.cs`, `Services/NotificationService.cs` |
| Unified Member inbox | `colearnx.client/src/components/MemberNotifications*.jsx`, `memberNotificationsState.js` |
| Password reset backend | `CoLearnX.Server/Controllers/AuthController.PasswordReset.cs`, `Services/PasswordResetService.cs`, `Services/PasswordResetMailSender.cs` |
| Password reset frontend | `colearnx.client/src/pages/ForgotPasswordPage.jsx`, `ResetPasswordPage.jsx` |
| Gmail setup | `scripts/Configure-GmailPasswordReset.ps1` |

New API additions retain camelCase JSON and the existing API client/error contracts:

- GET `/api/certificates/eligibility`
- GET `/api/certificates/requests/my`
- GET `/api/notifications/my`
- PUT `/api/notifications/{id}/read`, PUT `/api/notifications/read-all`
- POST `/api/auth/forgot-password`, POST `/api/auth/reset-password`

## Fresh checks on 2026-09-18

| Check | Result |
| --- | --- |
| `dotnet test CoLearnX.Server.Tests/CoLearnX.Server.Tests.csproj --no-restore --verbosity quiet --logger 'trx;LogFileName=member-features.trx'` | 205 passed, 0 failed |
| `npm.cmd run test:node` | 19 passed |
| `npm.cmd run test:ui -- --reporter=dot` | 23 passed across 12 files |
| `npm.cmd run build` | passed |
| Targeted ESLint: new components/pages, Member pages, routing, API and UTC helper | passed |
| `git diff --check` | passed |
| Full repository `npm.cmd run lint` | baseline 3 errors / 1 warning remain: LoginPage and AdminLoginPage set-state-in-effect; RoleApplicationsPanel mixed exports and effect dependencies |

Backend TRX evidence is in ignored `CoLearnX.Server.Tests/TestResults/member-features.trx`.
Existing dependency advisory warnings remain in the baseline NuGet/npm graph;
dependency upgrades were not included in this feature change.

## Real local browser flow

Backend: http://localhost:5088 (`/health` returned ok).
Frontend: https://localhost:55128.
Database: `CoLearnX.Server/App_Data/colearnx-member-next-local.db`.

Seed accounts use `Password123!`:

- Member: `huang.yousheng@colearnx.com`
- Trainer: `gu.yincheng@colearnx.com`
- Admin: `zhu.zirui@colearnx.com`, separate `/admin/login`

Verified in Chrome:

1. Member certificate page shows four pre-existing seeded certificates and
   ineligible-course explanations. Those four are baseline fixtures, not new awards.
2. Eligible INFT 2051 enrollment #1 submitted via UI; request #1 appeared immediately
   as Awaiting Trainer review, and one unread notification appeared.
3. Notification marked read; subsequent sign-in retained its read state.
4. Trainer Learner List approved request #1 with a note.
5. Admin Approvals > Certificates showed TrainerApproved and then issued it.
6. Member signed in again: five certificates (the four fixtures plus new INFT 2051
   certificate), Issued state, both notes and correctly localized timestamps.
7. Inbox showed exactly three workflow notices; mark-all-read changed count to 0;
   internal business link returned to certificates; browser refresh retained 0.

Local preparation boundary: the baseline does not provide a completion-edit API,
and the existing attendance editor only accepts an enrollment's selected Session,
whereas certification counts every Session in the Intake. For this new isolated
demo database only, `scripts/Prepare-MemberCertificateDemo.py` marks the seeded
INFT 2051 enrollment complete and inserts missing attendance fixtures. Assessment
score 88 was entered via the real Trainer API. Certificate submission and both
reviews were performed through browser UI; no certificate was inserted manually.
This does not claim the earlier completion/attendance editor gap has been fixed.

On 2026-09-19 at 16:36 Asia/Shanghai, a real browser forgot-password request sent
"Reset your CoLearnX password" through Gmail SMTP to jianglingmuse@gmail.com.
Receipt was verified in Gmail. The email link opened the local reset form and
the token fragment disappeared from the address bar. User entry/submission of
the new password is pending; completed reset, new-password login and rejection
of that local account's old credentials are not yet claimed as real-flow evidence.

## Review boundaries

The current diff was checked for ownership, ordinary/Admin identity separation,
role revocation, controlled notification links, credential storage/logging,
single-use/concurrency, transaction atomicity and old-database compatibility.
No production deployment, public verification site, PDF export, independent badge
award engine or live SQL Server acceptance is included.
