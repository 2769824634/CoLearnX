import CreatorPageFrame from './CreatorPageFrame';

export default function CreatorUploadPage() {
  return (
    <CreatorPageFrame
      eyebrow="Content library"
      title="Material upload workspace"
      description="Prepare learning materials for the shared upload service."
    >
      <div className="callout info">
        <div className="callout-title">No material selected</div>
        Uploaded learning materials will appear in your content library.
      </div>
    </CreatorPageFrame>
  );
}
