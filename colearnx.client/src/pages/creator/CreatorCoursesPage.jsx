import CreatorPageFrame from './CreatorPageFrame';

export default function CreatorCoursesPage() {
  return (
    <CreatorPageFrame
      eyebrow="Course management"
      title="Course workspace"
      description="Create, review, and submit your courses from one workspace."
    >
      <div className="callout info">
        <div className="callout-title">No courses to display</div>
        Your draft and submitted courses will appear here.
      </div>
    </CreatorPageFrame>
  );
}
