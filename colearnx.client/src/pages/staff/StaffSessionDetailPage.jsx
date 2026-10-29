import { useParams } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import { staffDeliveryApi } from '../../api/trainerDelivery';
import { staffLaterPhaseApi } from '../../api/trainerLaterPhase';
import useStaffAuth from '../../auth/useStaffAuth';
import TrainerSessionDetailPage from '../trainer/TrainerSessionDetailPage';

async function loadStaffSessionWorkspace(token, key, signal) {
  const [intakeId, sessionId] = key.split(':').map(Number);
  const intake = await staffCoursesApi.getIntake(token, intakeId, signal);
  const [course, learners, recordings] = await Promise.all([
    staffCoursesApi.get(token, intake.courseId, signal),
    staffLaterPhaseApi.learners(token, intakeId, signal),
    staffLaterPhaseApi.recordings(token, intakeId, sessionId, signal),
  ]);
  const session = intake.sessions.find((item) => item.id === sessionId);
  if (!session) throw new Error('Session not found.');
  return { intake, course, session, learners, recordings };
}

export default function StaffSessionDetailPage() {
  const { token } = useStaffAuth();
  const { courseIntakeId } = useParams();
  return <TrainerSessionDetailPage
    token={token}
    laterPhaseApi={staffLaterPhaseApi}
    deliveryApi={staffDeliveryApi}
    loadWorkspace={loadStaffSessionWorkspace}
    backPath={`/staff/intakes/${courseIntakeId}`}
  />;
}
