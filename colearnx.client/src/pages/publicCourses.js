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
  const cached = peek(key);
  const [courses, setCourses] = useState(() => (Array.isArray(cached) ? cached : []).map(mapPublicCourse));
  const [loading, setLoading] = useState(!Array.isArray(cached));
  const [error, setError] = useState('');

  useEffect(() => {
    const hit = peek(key);
    if (Array.isArray(hit)) {
      setCourses(hit.map(mapPublicCourse));
      setLoading(false);
      setError('');
      return undefined;
    }
    let active = true;
    setLoading(true);
    loadOnce(key, () => coursesApi.list())
      .then((items) => { if (active) setCourses(items.map(mapPublicCourse)); })
      .catch((reason) => { if (active) setError(userFacingError(reason, 'Could not load programs.')); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [key]);

  return { courses, loading, error };
}
