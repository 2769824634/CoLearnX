import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { authApi, certificatesApi, coursesApi, creditsApi, enrollmentsApi, usersApi } from '../../api';
import { invalidate, loadOnce, peek, put, courseListKey } from '../../api/readCache';
import { useAuth } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import { formatUtcDate, formatUtcRange, userFacingError } from '../businessPresentation';

// Loads member catalog / enrollments / credits from API.
function mapCourseListItem(c) {
  return {
    id: c.id,
    code: c.code,
    title: c.title,
    trainer: c.trainerName,
    creatorName: c.creatorName,
    trainerNames: c.trainerNames || [],
    credits: c.creditCost,
    level: c.level,
    topic: c.category,
    category: c.category,
    featured: c.isFeatured,
    interests: c.interests || [],
    averageStars: c.averageStars,
    ratingCount: c.ratingCount || 0,
    wishlisted: c.inWishlist,
    sessions: [],
  };
}

function mapCourseDetail(c) {
  return {
    id: c.id,
    code: c.code,
    title: c.title,
    trainer: c.trainerName,
    creatorName: c.creatorName,
    trainerNames: c.trainerNames || [],
    learningPath: c.learningPath,
    credits: c.creditCost,
    level: c.level,
    topic: c.category,
    category: c.category,
    description: c.description,
    outcomes: c.learningOutcomes || [],
    inWishlist: c.inWishlist,
    alreadyEnrolled: c.alreadyEnrolled,
    interests: c.interests || [],
    averageStars: c.averageStars,
    ratingCount: c.ratingCount || 0,
    sessions: (c.sessions || []).map((s) => ({
      id: s.id,
      label: s.label,
      when: formatUtcRange(s.startsAt, s.endsAt),
      seats: s.seatsLeft,
      capacity: s.physicalCapacity ?? s.capacity ?? 0,
      physical: Boolean(s.physicalAddress),
      online: Boolean(s.meetingLink),
      startsAt: s.startsAt,
      endsAt: s.endsAt,
      registrationOpensAt: s.registrationOpensAt,
      registrationClosesAt: s.registrationClosesAt,
      physicalBookingDeadline: s.physicalBookingDeadline,
      minEnrollment: s.minEnrollment ?? 10,
      intakeStatus: s.intakeStatus,
      intakeEnrollmentCount: s.intakeEnrollmentCount,
      confirmedToRunAt: s.confirmedToRunAt,
      cancelledAt: s.cancelledAt,
    })),
  };
}

function mapEnrollment(e) {
  return {
    id: e.courseId,
    enrollmentId: e.id,
    progress: e.progressPercent,
    status: e.status.toLowerCase(),
    courseCode: e.courseCode,
    courseTitle: e.courseTitle,
    trainer: e.trainerName,
    sessionId: e.courseSessionId,
    meetingLink: e.meetingLink,
    heldCredits: e.heldCredits ?? 0,
    registrationClosesAt: e.registrationClosesAt,
    startsAt: e.startsAt,
    withdrawalRefundCredits: e.withdrawalRefundCredits ?? null,
    postponementOptions: e.postponementOptions ?? [],
    cert: e.status === 'Completed' ? 'Earned' : undefined,
  };
}

function mapPackage(p) {
  return {
    id: p.id,
    credits: p.credits,
    price: `$${Number(p.payAud).toFixed(0)}`,
    note: p.note,
    best: p.isBestValue,
  };
}

function mapLedgerItem(l) {
  return {
    id: l.id,
    date: formatUtcDate(l.createdAt),
    type: l.type,
    desc: l.description,
    delta: l.delta,
    balance: l.balanceAfter,
    heldAfter: l.heldAfter,
  };
}

function fetchSlice(name) {
  if (name === 'catalog') return coursesApi.list();
  if (name === 'enrollments') return enrollmentsApi.my();
  if (name === 'billing') {
    return Promise.all([creditsApi.packages(), creditsApi.myLedger()])
      .then(([packages, ledger]) => ({ packages, ledger }));
  }
  if (name === 'certificates') return certificatesApi.my();
  return Promise.resolve(null);
}

function sliceView(name, data) {
  if (name === 'catalog' && Array.isArray(data)) {
    return {
      courses: data.map(mapCourseListItem),
      wishlist: data.filter((course) => course.inWishlist).map((course) => course.id),
    };
  }
  if (name === 'enrollments' && Array.isArray(data)) return { enrolled: data.map(mapEnrollment) };
  if (name === 'billing' && data) {
    return {
      packages: (data.packages || []).map(mapPackage),
      ledger: (data.ledger || []).map(mapLedgerItem),
    };
  }
  if (name === 'certificates' && Array.isArray(data)) return { certificates: data };
  return null;
}

function applySlice(name, data, setters) {
  const view = sliceView(name, data);
  if (!view) return;
  if ('courses' in view) {
    setters.setCourses(view.courses);
    setters.setWishlist(view.wishlist);
  }
  if ('enrolled' in view) setters.setEnrolled(view.enrolled);
  if ('packages' in view) {
    setters.setPackages(view.packages);
    setters.setLedger(view.ledger);
  }
  if ('certificates' in view) setters.setCertificates(view.certificates);
}

const sliceNames = ['catalog', 'enrollments', 'billing', 'certificates'];
const emptyMember = { courses: [], wishlist: [], enrolled: [], packages: [], ledger: [], certificates: [] };

function sliceKey(token, name) {
  if (name === 'catalog') return courseListKey(token);
  return `${name}:${token || ''}`;
}

function memberSnapshot(token) {
  const slices = {};
  const view = { ...emptyMember };
  for (const name of sliceNames) {
    const mapped = sliceView(name, peek(sliceKey(token, name)));
    slices[name] = mapped ? 'ready' : 'idle';
    if (mapped) Object.assign(view, mapped);
  }
  return { ...view, slices };
}

export function MemberDataProvider({ children }) {
  const { user, refreshUser, token } = useAuth();
  const boot = memberSnapshot(token);
  const [courses, setCourses] = useState(boot.courses);
  const [enrolled, setEnrolled] = useState(boot.enrolled);
  const [packages, setPackages] = useState(boot.packages);
  const [ledger, setLedger] = useState(boot.ledger);
  const [certificates, setCertificates] = useState(boot.certificates);
  const [wishlist, setWishlist] = useState(boot.wishlist);
  const [slices, setSlices] = useState(boot.slices);
  const [sliceErrors, setSliceErrors] = useState({});
  const [toast, setToast] = useState('');
  const [insufficientOpen, setInsufficientOpen] = useState(false);

  const showToast = useCallback((msg) => {
    if (/not enough credits/i.test(String(msg || ''))) {
      setInsufficientOpen(true);
      return;
    }
    setToast(msg);
  }, []);
  const closeInsufficientCredits = useCallback(() => setInsufficientOpen(false), []);

  useEffect(() => {
    if (!toast) return undefined;
    const t = setTimeout(() => setToast(''), /paypal|capture|top-up|timed out/i.test(toast) ? 12000 : 2200);
    return () => clearTimeout(t);
  }, [toast]);

  const statusRef = useRef(boot.slices);
  const tokenRef = useRef(token);
  if (tokenRef.current !== token) {
    tokenRef.current = token;
    const next = memberSnapshot(token);
    statusRef.current = next.slices;
    setCourses(next.courses);
    setEnrolled(next.enrolled);
    setPackages(next.packages);
    setLedger(next.ledger);
    setCertificates(next.certificates);
    setWishlist(next.wishlist);
    setSlices(next.slices);
  }
  const generationRef = useRef(Object.fromEntries(sliceNames.map((name) => [name, 0])));
  const requestedRef = useRef([]);
  const loadSliceRef = useRef(async () => {});

  const updateStatus = useCallback((name, status) => {
    statusRef.current = { ...statusRef.current, [name]: status };
    setSlices((prev) => (prev[name] === status ? prev : { ...prev, [name]: status }));
  }, []);

  const loadSlice = useCallback(async (name, { silent, fresh = false }) => {
    const generation = generationRef.current[name] + 1;
    generationRef.current[name] = generation;
    const key = sliceKey(token, name);
    if (!silent) {
      updateStatus(name, 'loading');
      setSliceErrors((prev) => ({ ...prev, [name]: '' }));
    }
    try {
      const factory = () => fetchSlice(name);
      if (fresh) invalidate(key);
      const data = fresh ? await factory() : await loadOnce(key, factory);
      if (generationRef.current[name] !== generation) return;
      if (fresh) put(key, data);
      applySlice(name, data, { setCourses, setWishlist, setEnrolled, setPackages, setLedger, setCertificates });
      updateStatus(name, 'ready');
      setSliceErrors((prev) => ({ ...prev, [name]: '' }));
    } catch (e) {
      if (generationRef.current[name] !== generation) return;
      const message = userFacingError(e, 'Could not load member data. Please retry.');
      if (silent && statusRef.current[name] === 'ready') {
        showToast(message);
        return;
      }
      setSliceErrors((prev) => ({ ...prev, [name]: message }));
      updateStatus(name, 'error');
      showToast(message);
    }
  }, [showToast, token, updateStatus]);

  loadSliceRef.current = loadSlice;

  const ensure = useCallback((names) => {
    for (const name of names) {
      if (!requestedRef.current.includes(name)) requestedRef.current = [...requestedRef.current, name];
      const status = statusRef.current[name];
      if (status === 'loading' || status === 'ready') continue;
      loadSliceRef.current(name, { silent: false, fresh: false });
    }
  }, []);

  const reload = useCallback(async (names) => {
    const target = names?.length ? names : requestedRef.current;
    await Promise.all(target.map((name) => loadSliceRef.current(name, { silent: statusRef.current[name] === 'ready', fresh: true })));
  }, []);

  const refreshSlice = useCallback(async (name) => {
    if (statusRef.current[name] === 'idle') return;
    await loadSliceRef.current(name, { silent: true, fresh: true });
  }, []);

  const state = useMemo(
    () => ({
      credits: user?.creditBalance ?? 0,
      heldCredits: user?.heldCredits ?? 0,
      totalCredits: user?.totalCredits ?? (user?.creditBalance ?? 0) + (user?.heldCredits ?? 0),
      user: {
        id: user?.id,
        fullName: user?.fullName ?? '',
        displayName: user?.displayName ?? '',
        email: user?.email ?? '',
        phone: user?.phone ?? '',
        bio: user?.bio ?? '',
        learningGoals: user?.learningGoals ?? '',
        specialisations: user?.specialisations ?? '',
        trainerHeadline: user?.trainerHeadline ?? '',
      },
      identityVisibility: {
        member: user?.identityVisibility?.member ?? true,
        trainer: user?.identityVisibility?.trainer ?? false,
        creator: user?.identityVisibility?.creator ?? false,
      },
      enrolled,
      wishlist,
      ledger,
      prefs: { emailNotif: true, darkMode: false },
      courses,
      packages,
      certificates,
      slices,
      sliceErrors,
      loading: false,
      loadError: '',
    }),
    [user, enrolled, wishlist, ledger, courses, packages, certificates, slices, sliceErrors],
  );

  const detailKey = useCallback((id) => `course:${token || 'guest'}:${id}`, [token]);
  const loadCourseDetail = useCallback(async (id, options) => {
    const key = detailKey(id);
    if (options?.fresh) invalidate(key);
    const raw = options?.fresh
      ? await coursesApi.get(id)
      : await loadOnce(key, () => coursesApi.get(id));
    if (options?.fresh) put(key, raw);
    return mapCourseDetail(raw);
  }, [detailKey]);
  const peekCourseDetail = useCallback((id) => {
    const raw = peek(detailKey(id));
    return raw ? mapCourseDetail(raw) : null;
  }, [detailKey]);

  async function enrol(courseId, sessionId) {
    const result = await enrollmentsApi.enrol(courseId, sessionId);
    refreshUser({ ...user, creditBalance: result.balanceAfter, heldCredits: result.heldAfter,
      totalCredits: result.balanceAfter + result.heldAfter });
    await refreshSlice('enrollments');
    return { ok: true, balance: result.balanceAfter, creditsSpent: result.creditsSpent,
      heldAfter: result.heldAfter, status: result.status };
  }

  async function changeEnrollment(id, action) {
    if (action === 'cancel') await enrollmentsApi.cancelReservation(id);
    else await enrollmentsApi.withdraw(id);
    refreshUser(await authApi.me());
    await refreshSlice('enrollments');
  }

  async function acceptPostponement(id, courseSessionId) {
    const result = await enrollmentsApi.acceptPostponement(id, courseSessionId);
    refreshUser({ ...user, creditBalance: result.balanceAfter, heldCredits: result.heldAfter,
      totalCredits: result.balanceAfter + result.heldAfter });
    await refreshSlice('enrollments');
  }

  async function applyLedgerTopUp(row) {
    const held = row.heldAfter ?? user?.heldCredits ?? 0;
    refreshUser({
      ...user,
      creditBalance: row.balanceAfter,
      heldCredits: held,
      totalCredits: row.balanceAfter + held,
    });
    if (statusRef.current.billing === 'loading') {
      generationRef.current.billing += 1;
      await loadSliceRef.current('billing', { silent: true, fresh: true });
      return;
    }
    setLedger((prev) => {
      const mapped = mapLedgerItem(row);
      const without = mapped.id == null ? prev : prev.filter((item) => item.id !== mapped.id);
      return [mapped, ...without];
    });
    const billingKey = sliceKey(token, 'billing');
    const billing = peek(billingKey);
    if (billing?.ledger) {
      const without = row.id == null ? billing.ledger : billing.ledger.filter((item) => item.id !== row.id);
      put(billingKey, { ...billing, ledger: [row, ...without] });
    }
  }

  async function saveProfile(draft) {
    const updated = await usersApi.update(user.id, {
      fullName: draft.user.fullName,
      displayName: draft.user.displayName,
      phone: draft.user.phone,
      bio: draft.user.bio,
      learningGoals: draft.user.learningGoals,
      emailNotifications: draft.prefs.emailNotif,
      darkMode: draft.prefs.darkMode,
      identityVisibility: draft.identityVisibility,
      specialisations: draft.user.specialisations,
      trainerHeadline: draft.user.trainerHeadline,
    });
    refreshUser(updated);
    showToast('Profile saved');
  }

  async function toggleWish(id) {
    try {
      const saved = wishlist.includes(id);
      const result = saved
        ? await coursesApi.removeWishlist(id)
        : await coursesApi.addWishlist(id);
      setWishlist((prev) =>
        result.inWishlist ? (prev.includes(id) ? prev : [...prev, id]) : prev.filter((x) => x !== id),
      );
      const catalogKey = sliceKey(token, 'catalog');
      const courseList = peek(catalogKey);
      if (Array.isArray(courseList)) {
        put(catalogKey, courseList.map((course) => (
          course.id === id ? { ...course, inWishlist: result.inWishlist } : course
        )));
      }
      showToast(result.inWishlist ? 'Saved to wishlist' : 'Removed from wishlist');
    } catch (e) {
      showToast(userFacingError(e, 'Could not update your wishlist. Please try again.'));
    }
  }

  const value = {
    state,
    toast,
    insufficientOpen,
    showToast,
    closeInsufficientCredits,
    changeEnrollment,
    acceptPostponement,
    reload,
    ensure,
    loadCourseDetail,
    peekCourseDetail,
    enrol,
    applyLedgerTopUp,
    saveProfile,
    toggleWish,
  };

  return <MemberDataContext.Provider value={value}>{children}</MemberDataContext.Provider>;
}
