import CreatorPageFrame from './CreatorPageFrame';

export default function CreatorHomePage() {
  return (
    <CreatorPageFrame
      eyebrow="Workspace status"
      title="Creator overview"
      description="Track the areas that will receive Creator course and content data."
    >
      <div className="grid-2">
        <div className="card">
          <div className="card-header">Courses</div>
          <div className="card-body">No course activity is available yet.</div>
        </div>
        <div className="card">
          <div className="card-header">Materials</div>
          <div className="card-body">No material activity is available yet.</div>
        </div>
      </div>
    </CreatorPageFrame>
  );
}
