import { useState } from 'react';
import { utcDate } from '../utils/utcDates';
import '../styles/session-calendar.css';

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const pad = (part) => String(part).padStart(2, '0');
const time = (date) => `${pad(date.getHours())}:${pad(date.getMinutes())}`;
const dayKey = (date) => `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
const monthIndex = (date) => date.getFullYear() * 12 + date.getMonth();
const toDate = (value) => (value instanceof Date ? value : utcDate(value));

function hoursLabel(totalMinutes) {
  const hours = Math.round((totalMinutes / 60) * 10) / 10;
  return `${hours} hour${hours === 1 ? '' : 's'}`;
}

// Monday-first weeks covering the whole month, padded with empty cells.
function monthCells(index) {
  const year = Math.floor(index / 12);
  const month = index % 12;
  const lead = (new Date(year, month, 1).getDay() + 6) % 7;
  const days = new Date(year, month + 1, 0).getDate();
  const cells = Array.from({ length: lead }, () => null);
  for (let day = 1; day <= days; day += 1) cells.push(new Date(year, month, day));
  while (cells.length % 7) cells.push(null);
  return Array.from({ length: cells.length / 7 }, (_, week) => cells.slice(week * 7, week * 7 + 7));
}

function clashText(clashes) {
  return clashes.map((clash) => `${clash.courseCode} ${clash.sessionLabel} ${time(toDate(clash.startsAt))}–${time(toDate(clash.endsAt))}`).join('; ');
}

// A month view of an Intake's sessions in the viewer's local time, so a Trainer can check the dates at a glance.
export default function SessionCalendar({ sessions = [], caption }) {
  const items = sessions
    .map((session) => ({ ...session, start: toDate(session.startsAt), end: toDate(session.endsAt), clashes: session.clashes || [] }))
    .filter((session) => Number.isFinite(session.start.getTime()))
    .sort((left, right) => left.start - right.start);
  const first = items.length ? monthIndex(items[0].start) : null;
  const last = items.length ? monthIndex(items[items.length - 1].start) : null;
  const [shown, setShown] = useState(first);
  if (!items.length) return <p className="session-calendar-empty">No sessions scheduled yet.</p>;

  // Keep the view inside the months that hold sessions, even after the list changes.
  const current = shown === null || shown < first || shown > last ? first : shown;
  const byDay = new Map();
  for (const item of items) byDay.set(dayKey(item.start), [...(byDay.get(dayKey(item.start)) || []), item]);
  const totalMinutes = items.reduce((sum, item) => sum + (item.end - item.start) / 60000, 0);
  const clashing = items.filter((item) => item.clashes.length).length;
  const title = `${MONTHS[current % 12]} ${Math.floor(current / 12)}`;
  const headingId = `session-calendar-${current}`;

  return <div className="session-calendar">
    <div className="session-calendar-bar">
      <button type="button" className="btn btn-ghost btn-sm" aria-label="Previous month" disabled={current <= first} onClick={() => setShown(current - 1)}>‹</button>
      <h3 id={headingId}>{title}</h3>
      <button type="button" className="btn btn-ghost btn-sm" aria-label="Next month" disabled={current >= last} onClick={() => setShown(current + 1)}>›</button>
    </div>
    <p className="session-calendar-summary">
      {caption ? <>{caption} · </> : null}
      {items.length} session{items.length === 1 ? '' : 's'} · {hoursLabel(totalMinutes)} in total · {items[0].start.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })} to {items[items.length - 1].start.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}
    </p>
    {clashing ? <p className="session-calendar-warning" role="alert">
      {clashing} session{clashing === 1 ? ' clashes' : 's clash'} with teaching you already have. They are marked in red.
    </p> : null}
    <table className="session-calendar-grid" role="grid" aria-labelledby={headingId}>
      <thead><tr>{WEEKDAYS.map((day) => <th key={day} scope="col">{day}</th>)}</tr></thead>
      <tbody>{monthCells(current).map((week, row) => <tr key={row}>{week.map((day, column) => {
        if (!day) return <td key={column} className="session-calendar-blank" aria-hidden="true" />;
        const daySessions = byDay.get(dayKey(day)) || [];
        const label = `${day.getDate()} ${MONTHS[day.getMonth()]} ${day.getFullYear()}${daySessions.length ? `, ${daySessions.length} session${daySessions.length === 1 ? '' : 's'}` : ''}`;
        return <td key={column} role="gridcell" aria-label={label} className={daySessions.length ? 'has-session' : undefined}>
          <span className="session-calendar-day" aria-hidden="true">{day.getDate()}</span>
          {daySessions.length ? <ul>{daySessions.map((item) => <li key={item.id ?? item.start.getTime()}
            className={item.clashes.length ? 'clash' : undefined}
            title={item.clashes.length ? `Clashes with ${clashText(item.clashes)}` : undefined}>
            <strong>{time(item.start)}–{time(item.end)}</strong>
            <span>{item.label}</span>
          </li>)}</ul> : null}
        </td>;
      })}</tr>)}</tbody>
    </table>
  </div>;
}
