import MemberShell from '../../components/MemberShell';
import { useMemberData } from './MemberDataContext';

// Badges / certificates. Shared: MemberBadgesPage
export default function MemberBadgesPage() {
  const { state, showToast } = useMemberData();
  const stages = state.certificates.length
    ? state.certificates.map((c) => ({ n: c.stageNumber, name: c.stageName, date: new Date(c.awardedAt).toLocaleDateString() }))
    : state.certStages;

  return (
    <MemberShell
      title="Badges & Certificates"
      subtitle="MBR-08 · Four-stage certificates earned on program completion"
      onNotify={() => showToast('3 notifications')}
    >
      <div className="stage-list">
        {stages.map((s) => (
          <div className="stage-item" key={s.n || s.name}>
            <div className="stage-num">{s.n}</div>
            <div style={{ flex: 1 }}>
              <strong>{s.name}</strong>
              <div style={{ fontSize: 12, color: 'var(--slate)' }}>Awarded {s.date}</div>
            </div>
            <span className="pill success">Earned</span>
          </div>
        ))}
      </div>
    </MemberShell>
  );
}
