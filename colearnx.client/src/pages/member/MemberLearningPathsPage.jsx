import { useState } from 'react';
import { Link } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { learningPathsApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { userFacingError } from '../businessPresentation';
import '../../styles/learning-paths.css';

const loadPaths = (token, _key, signal) => learningPathsApi.my(token, signal);

export default function MemberLearningPathsPage() {
  const query = useTrainerQuery(loadPaths, 'learning-paths');
  const [selected, setSelected] = useState('');
  const paths = query.data || [];
  const path = paths.find((item) => String(item.id) === selected)
    || paths.find((item) => item.stages.some((stage) => stage.completedCourseCount > 0))
    || paths.find((item) => item.stages.some((stage) => stage.courses.length > 0)) || paths[0];

  return <MemberShell title="My Learning Paths" subtitle="Choose a subject and explore the next step in your learning.">
    {query.loading ? <p role="status">Loading learning paths…</p> : null}
    {query.error ? <div className="callout warn" role="alert">{userFacingError(query.error, 'Could not load learning paths.')} <button className="btn btn-ghost" type="button" onClick={query.refresh}>Retry</button></div> : null}
    {!query.loading && !query.error && !paths.length ? <p>No learning paths are available yet.</p> : null}
    {path ? <div className="learning-path-page">
      <div className="learning-path-intro">
        <div><p className="learning-path-eyebrow">Your learning journey</p><h2>{path.name}</h2>
          <p>Earn course certificates to complete each stage. You can enrol in any stage at any time.</p></div>
        <label>Subject <select value={String(path.id)} onChange={(event) => setSelected(event.target.value)}>
          {paths.map((item) => <option key={item.id} value={String(item.id)}>{item.name}</option>)}
        </select></label>
      </div>
      <div className="learning-path-summary"><strong>{path.stages.filter((stage) => stage.completed).length} of 3 stages completed</strong>
        <span>{path.graduated ? 'Graduation certificate earned' : 'Complete all three stages to earn a Graduation certificate.'}</span>
        <Link to="/member/badges">View certificates →</Link>
      </div>
      <div className="learning-path-stages">{path.stages.map((stage) => <section className="learning-path-stage" key={stage.number} aria-labelledby={`stage-${stage.number}`}>
        <div className="learning-path-stage-heading"><div><span className="learning-path-number">0{stage.number}</span><h3 id={`stage-${stage.number}`}>{stage.name}</h3></div>
          <span className={`pill ${stage.completed ? 'success' : 'neutral'}`}>{stage.completed ? 'Completed' : `${stage.completedCourseCount} / ${stage.requiredCourseCount} courses`}</span></div>
        {stage.suggestedPrerequisite ? <p className="learning-path-hint">Suggested first: {stage.suggestedPrerequisite}</p> : <p className="learning-path-hint">{stage.completed ? 'Stage certificate earned.' : 'A good place to start.'}</p>}
        {stage.courses.length ? <ul className="learning-path-courses">{stage.courses.map((course) => <li key={course.id}>
          <Link to={`/member/courses/${course.id}`}><strong>{course.title}</strong><span>{course.code} · {course.credits} credits</span></Link>
          <span>{course.completed ? 'Completed' : course.enrolled ? 'Enrolled' : course.hasClass ? 'View course →' : 'No class scheduled'}</span>
        </li>)}</ul> : <p className="learning-path-empty">No published courses in this stage yet.</p>}
      </section>)}</div>
    </div> : null}
  </MemberShell>;
}
