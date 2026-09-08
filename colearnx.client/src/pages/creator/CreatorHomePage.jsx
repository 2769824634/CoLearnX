import { Link } from 'react-router-dom';
import { CreatorHeader } from './CreatorUi';

export default function CreatorHomePage() {
  return <section className="creator-page"><CreatorHeader title="Creator workspace">Review Intake applications for the Courses you own without changing Trainer-authored schedules.</CreatorHeader>
    <Link className="creator-review-card" to="/creator/courses"><span className="creator-eyebrow">B4 review queue</span><strong>Open Intake applications</strong><p>Compare the live schedule with any proposed structural change, then confirm or return it with a note.</p><span>Go to Courses →</span></Link>
  </section>;
}
