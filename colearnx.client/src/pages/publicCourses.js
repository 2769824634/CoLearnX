import { useEffect, useState } from 'react';
import { loadOnce, peek, courseListKey } from '../api/readCache';
import { coursesApi } from '../api';
import { useAuth } from '../auth/AuthContext';
import { userFacingError } from './businessPresentation';

export function mapPublicCourse(c) {
  return {
    id: c.id,
    code: c.code,
    title: c.title,
    trainer: c.trainerName,
    creatorName: c.creatorName,
    trainerNames: c.trainerNames || [],
    credits: c.creditCost,
    level: c.level,
    category: c.category,
    topic: c.category,
    description: c.description,
    outcomes: c.learningOutcomes || [],
    interests: c.interests || [],
    averageStars: c.averageStars,
    ratingCount: c.ratingCount || 0,
    sessions: c.sessions || [],
  };
}

export function usePublicCourses() {
  const { token } = useAuth();
  const key = courseListKey(token);
  const [trackedKey, setTrackedKey] = useState(key);
  const [courses, setCourses] = useState(() => {
    const cached = peek(key);
    return (Array.isArray(cached) ? cached : []).map(mapPublicCourse);
  });
  const [loading, setLoading] = useState(() => !Array.isArray(peek(key)));
  const [error, setError] = useState('');

  if (trackedKey !== key) {
    const hit = peek(key);
    setTrackedKey(key);
    setCourses((Array.isArray(hit) ? hit : []).map(mapPublicCourse));
    setLoading(!Array.isArray(hit));
    setError('');
  }

  useEffect(() => {
    if (Array.isArray(peek(key))) return undefined;
    let active = true;
    loadOnce(key, () => coursesApi.list())
      .then((items) => { if (active) setCourses(items.map(mapPublicCourse)); })
      .catch((reason) => { if (active) setError(userFacingError(reason, 'Could not load programs.')); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [key]);

  return { courses, loading, error };
}
