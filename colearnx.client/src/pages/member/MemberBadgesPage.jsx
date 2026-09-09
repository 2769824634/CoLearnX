import MemberShell from '../../components/MemberShell';
import { useMemberData } from './memberDataState';

export default function MemberBadgesPage() {
  const { showToast } = useMemberData();

  return (
    <MemberShell
      title="Badges & Certificates"
      onNotify={() => showToast('No new notifications')}
    />
  );
}
