"""Prepare only the isolated Member demo database created for this feature verification.

The application has no completion-edit endpoint. This fixture marks the seeded
UI/UX enrollment complete and supplies its Intake attendance; grades and both
reviews still use real APIs. The existing attendance editor accepts only the
enrollment's selected Session, although certification counts all Intake Sessions.
It never creates certificate requests, certificates or notifications.
"""
from pathlib import Path
import sqlite3

database = Path(__file__).resolve().parents[1] / "CoLearnX.Server/App_Data/colearnx-member-next-local.db"
if not database.is_file():
    raise SystemExit("Start the local feature server first; the isolated database is missing.")
with sqlite3.connect(database) as connection:
    row = connection.execute("""
        SELECT e.Id FROM Enrollments e JOIN Users u ON u.Id=e.UserId
        JOIN Courses c ON c.Id=e.CourseId
        WHERE u.Email='huang.yousheng@colearnx.com' AND c.Code='INFT 2051'
    """).fetchone()
    if row is None:
        raise SystemExit("Expected local seed enrollment not found; no data changed.")
    connection.execute("UPDATE Enrollments SET Status=1, ProgressPercent=100, CompletedAt=datetime('now') WHERE Id=?", row)
    connection.execute("""
        INSERT INTO AttendanceRecords (CourseSessionId,UserId,Status,RecordedByTrainerId,RecordedAt)
        SELECT s.Id,e.UserId,0,i.TrainerId,datetime('now') FROM Enrollments e
        JOIN CourseSessions selected ON selected.Id=e.CourseSessionId
        JOIN CourseIntakes i ON i.Id=selected.CourseIntakeId
        JOIN CourseSessions s ON s.CourseIntakeId=i.Id WHERE e.Id=?
        AND NOT EXISTS (SELECT 1 FROM AttendanceRecords a WHERE a.CourseSessionId=s.Id AND a.UserId=e.UserId)
    """, row)
    print(f"Local demo enrollment #{row[0]} completion/attendance prepared. Supply grades using Trainer APIs.")
