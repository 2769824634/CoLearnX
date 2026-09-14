import { Link } from 'react-router-dom';
import { CreatorHeader } from './CreatorUi';

export default function CreatorHomePage() {
  return (
    <section className="creator-page">
      <CreatorHeader title="Creator workspace">
        Create Courses, submit them for Admin approval, and upload materials for the Trainer library.
      </CreatorHeader>
      <Link className="creator-review-card" to="/creator/courses/new">
        <span className="creator-eyebrow">Course definition</span>
        <strong>Create Course</strong>
        <p>Save a draft, attach materials, then submit it so Admin can publish it to the catalogue.</p>
        <span>Open Create Course →</span>
      </Link>
      <Link className="creator-review-card" to="/creator/upload">
        <span className="creator-eyebrow">Learning materials</span>
        <strong>Upload Material</strong>
        <p>Upload PDF, PPTX, DOCX or images onto a Course.</p>
        <span>Open Upload →</span>
      </Link>
      <Link className="creator-review-card" to="/creator/courses/intake-applications">
        <span className="creator-eyebrow">B4 review queue</span>
        <strong>Intake applications</strong>
        <p>Confirm or return Trainer schedules submitted against Courses you own.</p>
        <span>Open review queue →</span>
      </Link>
    </section>
  );
}
