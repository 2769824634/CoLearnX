# Intake settlement, cancellation and postponement

Implemented in `20260929_v7`. Course -> CourseIntake -> CourseSession ownership and Creator review remain in place.

## Rules

- `MinEnrollment` defaults to 10, accepts 2-200, and cannot exceed a physical Session's capacity. Online capacity 0 remains unlimited.
- UTC timestamps satisfy `RegistrationOpensAt < RegistrationClosesAt <= StartsAt - 10 days` and `StartsAt < EndsAt`. Create, edit and structural proposals validate the same registration cutoff.
- The hosted worker scans at startup and once per minute. On a due published Intake it processes all Reserved enrollments in one serializable transaction: enough learners -> Capture/Active/ConfirmedToRunAt; too few -> Release/Cancelled/CancelledAt. A failed wallet or seat update rolls back the whole class. Previously charged historical Active enrollments are not charged again.
- A confirmed Intake remains Published until delivery starts, then the worker moves it to InProgress. It cannot accept new reservations after financial confirmation.
- The owning Trainer may cancel a Published or InProgress class. Reserved places receive 100% Release; Active places receive 100% Refund. Repeating cancellation does not return credits twice.
- Unconfirmed learners may cancel their Reserved place for 100% Release. For Active places, self-withdrawal is available only 6-10 UTC calendar dates before Intake StartsAt. The refund is 70%, rounded to the nearest whole credit with halves away from zero; the remainder is recorded as Forfeit with zero additional wallet debit. At <=5 or >10 days the endpoint rejects withdrawal. Legacy charged Active enrollments follow the same withdrawal rule.
- Structural changes cannot reopen the schedule once there are Reserved/Active enrollments or financial confirmation. Routine meeting-link updates remain available.

## Postponement

Only a class cancelled because it missed minimum enrollment (`MinimumEnrollmentNotMet`) can offer a postponement. Trainer-initiated cancellations use `TrainerCancelled` and do not grant this invitation.

1. Within seven elapsed days after `CancelledAt`, its owner creates one replacement Draft for the same Course and Trainer. The replacement starts later than the original. Sessions are copied with delivery and physical booking timestamps shifted by the new start; attendance, enrollments and resource history are not copied.
2. The Trainer can edit the Draft and submit it through the existing Creator review. Creator confirmation must also occur within the original seven-day window. Only after publication are eligible learners notified and offered replacement Sessions in My Programs / History.
3. A learner released by that class may accept one replacement within the same window, while registration is open and capacity is available. The normal available-credit and duplicate-course checks apply. A new Reserved enrollment holds the **original enrollment's credit price**, preserving the original price snapshot even if the Course price changed. No separate postponement fee is charged.
4. `PostponedFromEnrollmentId` preserves the original-to-new enrollment link and is unique. Repeating acceptance, including after cancelling the new reservation, cannot consume the invitation again. The learner may cancel the new reservation before confirmation for a full Release; later self-withdrawal uses the replacement Intake's start date.
5. A learner who cancelled independently before the class failed is not granted an invitation. Declining or allowing the window to expire requires no action; released credits stay available.

## API Extensions

| Endpoint | Request | Result |
| --- | --- | --- |
| `POST /api/trainer/intakes/{id}/cancel` | none | Settlement summary; owner only |
| `POST /api/trainer/intakes/{id}/postpone` | Existing CreateCourseIntakeRequest schedule | 201 replacement Draft with copied Sessions |
| `POST /api/enrollments/{id}/accept-postponement` | `{ "courseSessionId": 123 }` | New reservation and wallet balances; original learner only |
| `POST /api/enrollments/{id}/cancel-reservation` | none | Cancelled enrollment |
| `POST /api/enrollments/{id}/withdraw` | none | Refunded enrollment |

Intake detail adds `cancellationReason`, `replacementForIntakeId`, `replacementIntakeId`, and `postponementAvailableUntil`. Existing minimum and financial timestamps remain. Enrollment responses include `postponementOptions` and nullable `withdrawalRefundCredits`; the server rechecks eligibility on every mutation.

## Persistence And Verification

`CreditReservationSchema.EnsureAsync` adds missing columns and unique indexes to existing SQLite/SQL Server databases at startup. It preserves existing balances and historical enrollments, without inventing retroactive postponement eligibility. SQLite in-place upgrade and repeated invocation have an integration test. SQL Server schema statements are included, but a live SQL Server upgrade was not verified on this machine.

Focused tests cover the 9/10 threshold across Sessions, exact-once financial effects, failed settlement rollback, owned cancellation, seven-day boundaries, unpublished/unrelated replacement rejection, insufficient balance, full capacity, duplicate acceptance, self-cancelled enrollment rejection, UTC withdrawal boundaries, deadline mutation protection, and delivery state transition.

Validation on 2026-09-29: focused server regression 108/108; frontend 19 Node + 37 UI tests; server and client builds passed; changed frontend files passed ESLint. Full server regression before the final two guard tests had 267 passes and three unrelated failures: two existing role tests expect single-role demo accounts, and one SQL Server retry test needs unavailable LocalDB. Full frontend lint reports six pre-existing errors and one warning in untouched files. Existing package advisory warnings remain.

Local preview uses a separate `CoLearnX.Server/App_Data/intake-preview.db`, API `http://localhost:5097`, and UI `https://localhost:55137`. It does not use the normal application database.

Browser verification: the worker cancelled the under-enrolled preview Intake #4 and released 35 credits; the Trainer created replacement #5 with copied Sessions and submitted it; Creator confirmation through the API published it; the Member selected it from History and confirmed a new reservation. The resulting available/held balances were 85/35 (previously 120/0), with one original Release and one replacement Hold. The browser error log was empty. Desktop screenshots were inspected; the browser's mobile screenshot capture failed, so mobile visual verification is not claimed. Preview services were stopped after verification to release build files; restart them with the commands below.

From the v7 root, use two terminals:

```powershell
dotnet run --project CoLearnX.Server --no-launch-profile -- --urls http://localhost:5097 --ConnectionStrings:Default "Data Source=App_Data/intake-preview.db"
```

```powershell
Set-Location colearnx.client
$env:ASPNETCORE_URLS = 'http://localhost:5097'
npm run dev -- --host localhost --port 55137 --strictPort
```
