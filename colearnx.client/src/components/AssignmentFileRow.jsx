import { fileFormat, formatFileSize } from '../utils/assignmentFiles';
import '../styles/assessment-submission.css';

// One document in an assignment submission: type tile, name, size and an action button.
export default function AssignmentFileRow({ name, format, size, note, action }) {
  const label = format || fileFormat(name);
  return <li className="cx-file-row">
    <span className="cx-file-icon" data-format={label} aria-hidden="true">{label}</span>
    <span className="cx-file-name"><strong title={name}>{name}</strong><small>{[formatFileSize(size), note].filter(Boolean).join(' · ')}</small></span>
    {action}
  </li>;
}
