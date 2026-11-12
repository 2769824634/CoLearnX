// One line at the top of a saved course: where it stands and the button for the next step.
export default function CourseNextStep({ step, onAction, disabled }) {
  if (!step) return null;
  return (
    <div className="callout creator-next-step" role="status">
      <strong>{step.text}</strong>
      {step.action ? <button type="button" className="btn btn-primary btn-sm" disabled={disabled} onClick={() => onAction(step.action)}>{step.label}</button> : null}
    </div>
  );
}
