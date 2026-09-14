import { creatorCoursesApi } from '../../api';
import UserAccountPage from '../account/UserAccountPage';
import useTrainerQuery from '../trainer/useTrainerQuery';
import '../../styles/creator-intakes.css';

const loadCourses = (token, _key, signal) => creatorCoursesApi.list(token, signal);

export default function CreatorAccountPage() {
  const query = useTrainerQuery(loadCourses);
  const courses = query.data || [];
  return (
    <section className="creator-page">
      <UserAccountPage
        eyebrow="Creator identity"
        extraKind="creator"
        summaryTitle="Creator Summary"
        summaryStats={[
          { label: 'Courses', value: query.loading ? '—' : String(courses.length) },
          { label: 'Published', value: query.loading ? '—' : String(courses.filter((item) => item.status === 'Published').length) },
        ]}
      />
    </section>
  );
}
