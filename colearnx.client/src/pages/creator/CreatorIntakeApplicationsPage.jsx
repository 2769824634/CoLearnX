import { creatorIntakeApplicationsApi } from '../../api/creatorIntakeApplications';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorApplicationRows, CreatorError, CreatorHeader } from './CreatorUi';

const loadCreatorApplications = (token, key, signal) => creatorIntakeApplicationsApi.list(token, signal);

export default function CreatorIntakeApplicationsPage() {
  const query = useTrainerQuery(loadCreatorApplications);
  const applications = query.data || [];
  const pending = applications.filter((item) => item.status === 'Pending');
  const reviewed = applications.filter((item) => item.status !== 'Pending');
  return <section className="creator-page">
    <CreatorHeader title="Session approvals" action={<span className="creator-queue-count">{query.loading ? 'Loading queue…' : query.error ? 'Queue unavailable' : `${pending.length} waiting`}</span>}>Approve the Trainer’s Intake and Sessions for Courses you own. Confirm publishes every Session in that submission; Admin does not review this queue.</CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading Creator queue…</div> : query.error ? <CreatorError error={query.error} onRetry={query.refresh} /> : <>
      <div className="creator-section-heading"><div><p className="creator-eyebrow">{pending.length ? 'Action required' : 'Queue up to date'}</p><h2>{pending.length ? 'Waiting for review' : 'No reviews required'}</h2></div></div><CreatorApplicationRows applications={pending} />
      {reviewed.length ? <><div className="creator-section-heading"><div><p className="creator-eyebrow">Decision history</p><h2>Recently reviewed</h2></div></div><CreatorApplicationRows applications={reviewed} /></> : null}
    </>}
  </section>;
}
