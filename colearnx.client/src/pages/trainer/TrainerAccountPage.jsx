import UserAccountPage from '../account/UserAccountPage';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import '../../styles/trainer.css';

export default function TrainerAccountPage() {
  const query = useTrainerQuery(loadTrainerOverview);
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
