import CreatorPageFrame from './CreatorPageFrame';

export default function CreatorUsagePage() {
  return (
    <CreatorPageFrame
      eyebrow="Material insights"
      title="Usage records workspace"
      description="Review where Creator materials are used once usage data is available."
    >
      <div className="callout info">
        <div className="callout-title">No usage records</div>
        Course and material usage will appear here when records are available.
      </div>
    </CreatorPageFrame>
  );
}
