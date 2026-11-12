import UserAccountPage from '../account/UserAccountPage';
import { useAuth } from '../../auth/AuthContext';
import { isUncertified } from '../../auth/certificationLock';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import '../../styles/trainer.css';

const loadNothing = async () => ({ intakes: [] });

export default function TrainerAccountPage() {
  const { user } = useAuth();
  const locked = isUncertified(user, 'trainer');
  const query = useTrainerQuery(locked ? loadNothing : loadTrainerOverview, locked ? 'locked' : '');
  const intakes = query.data?.intakes || [];
  return (
    <section className="trainer-page">
      <UserAccountPage
        eyebrow="Trainer identity"
        extraKind="trainer"
        summaryTitle="Trainer Summary"
        summaryStats={[
          { label: 'Intakes', value: query.loading ? '—' : String(intakes.length) },
          { label: 'Published', value: query.loading ? '—' : String(intakes.filter((item) => item.status === 'Published' || item.status === 'InProgress').length) },
        ]}
      />
    </section>
  );
}
