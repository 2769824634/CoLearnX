import MemberShell from '../../components/MemberShell';
import { useMemberData } from './MemberDataContext';

// Badges / certificates.
export default function MemberBadgesPage() {
  const { state, showToast } = useMemberData();
  const stages = state.certificates.map((c) => ({
    n: c.stageNumber,
    name: c.stageName,
    date: new Date(c.awardedAt).toLocaleDateString(),
  }));

  return (
    <MemberShell
      title="Badges & Certificates"
      onNotify={() => showToast('No new notifications')}
    >
      {stages.length === 0 ? (
        <div className="callout">
          <div className="callout-title">No certificates yet</div>
          Complete a program to earn badges and certificates.
        </div>
      ) : (
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
      )}
    </MemberShell>
  );
}
