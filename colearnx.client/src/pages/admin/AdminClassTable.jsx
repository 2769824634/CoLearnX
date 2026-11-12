import {
  attendanceText, classStatusLabel, classStatusPill, classDates, isLowAttendance, timeLeftText,
} from './adminClasses';

// Shared by the Classes page and the Trainer / Creator tabs of a user's details.
export default function AdminClassTable({ rows, onOpen, hideTrainer = false, hideCreator = false, emptyText = 'No intakes match these filters.' }) {
  return <div className="later-table-wrap admin-class-table">
    <table className="later-table">
      <thead><tr>
        <th>Course</th>
        {hideCreator ? null : <th>Creator</th>}
        {hideTrainer ? null : <th>Trainer</th>}
        <th>Start / end</th><th>Time left</th><th>Learners</th><th>Attendance</th><th>Status</th>
      </tr></thead>
      <tbody>{rows.map((row) => {
        const low = isLowAttendance(row.attendanceRate);
        return <tr key={row.id} className={low ? 'admin-class-low' : undefined}>
          <td>
            {onOpen
              ? <button type="button" className="account-name-button" onClick={() => onOpen(row.id)}>{row.courseTitle}</button>
              : <strong>{row.courseTitle}</strong>}
            <small>{row.courseCode} · Intake #{row.id}</small>
          </td>
          {hideCreator ? null : <td>{row.creatorName}</td>}
          {hideTrainer ? null : <td>{row.trainerName}</td>}
          <td className="admin-nowrap">{classDates(row).start}<small>to {classDates(row).end}</small></td>
          <td className="admin-nowrap">{timeLeftText(row)}</td>
          <td>{row.learners}<small className="admin-nowrap">min. {row.minEnrollment}</small></td>
          <td className={low ? 'later-negative' : undefined}>
            {attendanceText(row.attendanceRate)}
            <small className="admin-nowrap">{row.sessionsHeld}/{row.sessionCount} sessions held</small>
          </td>
          <td><span className={classStatusPill(row.status)}>{classStatusLabel(row.status)}</span></td>
        </tr>;
      })}</tbody>
    </table>
    {!rows.length ? <div className="admin-review-state">{emptyText}</div> : null}
  </div>;
}
