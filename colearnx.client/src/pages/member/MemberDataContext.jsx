import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { authApi, certificatesApi, coursesApi, creditsApi, enrollmentsApi, usersApi } from '../../api';
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

export function MemberDataProvider({ children }) {
  const { user, refreshUser } = useAuth();
  const refreshUserRef = useRef(refreshUser);
  useEffect(() => { refreshUserRef.current = refreshUser; }, [refreshUser]);
  const [courses, setCourses] = useState([]);
  const [enrolled, setEnrolled] = useState([]);
  const [packages, setPackages] = useState([]);
  const [ledger, setLedger] = useState([]);
  const [certificates, setCertificates] = useState([]);
  const [wishlist, setWishlist] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
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

  const reload = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const [me, courseList, myEnrol, pkgs, myLedger, certs] = await Promise.all([
        authApi.me(),
        coursesApi.list(),
        enrollmentsApi.my(),
        creditsApi.packages(),
        creditsApi.myLedger(),
        certificatesApi.my(),
      ]);
      refreshUserRef.current(me);
      setCourses(courseList.map(mapCourseListItem));
      setWishlist(courseList.filter((c) => c.inWishlist).map((c) => c.id));
      setEnrolled(
        myEnrol.map((e) => ({
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
        })),
      );
      setPackages(
        pkgs.map((p) => ({
          id: p.id,
          credits: p.credits,
          price: `$${Number(p.payAud).toFixed(0)}`,
          note: p.note,
          best: p.isBestValue,
        })),
      );
      setLedger(
        myLedger.map((l) => ({
          date: formatUtcDate(l.createdAt),
          type: l.type,
          desc: l.description,
          delta: l.delta,
          balance: l.balanceAfter,
          heldAfter: l.heldAfter,
        })),
      );
      setCertificates(certs);
      return true;
    } catch (e) {
      const message = userFacingError(e, 'Could not load member data. Please retry.');
      setLoadError(message);
      showToast(message);
      return false;
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    const timer = setTimeout(reload, 0);
    return () => clearTimeout(timer);
  }, [reload]);

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
      loading,
      loadError,
    }),
    [user, enrolled, wishlist, ledger, courses, packages, certificates, loading, loadError],
  );

  const loadCourseDetail = useCallback(async (id) => {
    const detail = await coursesApi.get(id);
    return mapCourseDetail(detail);
  }, []);

  async function enrol(courseId, sessionId) {
    const result = await enrollmentsApi.enrol(courseId, sessionId);
    refreshUser({ ...user, creditBalance: result.balanceAfter, heldCredits: result.heldAfter,
      totalCredits: result.balanceAfter + result.heldAfter });
    let memberDataRefreshed;
    try {
      memberDataRefreshed = await reload();
    } catch {
      memberDataRefreshed = false;
    }
    return { ok: true, enrollmentId: result.enrollmentId, balance: result.balanceAfter, creditsSpent: result.creditsSpent,
      heldAfter: result.heldAfter, status: result.status, memberDataRefreshed };
  }

  async function changeEnrollment(id, action) {
    if (action === 'cancel') await enrollmentsApi.cancelReservation(id);
    else await enrollmentsApi.withdraw(id);
    refreshUser(await authApi.me());
    await reload();
  }

  async function acceptPostponement(id, courseSessionId) {
    const result = await enrollmentsApi.acceptPostponement(id, courseSessionId);
    refreshUser({ ...user, creditBalance: result.balanceAfter, heldCredits: result.heldAfter,
      totalCredits: result.balanceAfter + result.heldAfter });
    await reload();
  }

  async function applyLedgerTopUp(row) {
    refreshUser({ ...user, creditBalance: row.balanceAfter });
    await reload();
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
    loadCourseDetail,
    enrol,
    applyLedgerTopUp,
    saveProfile,
    toggleWish,
  };

  return <MemberDataContext.Provider value={value}>{children}</MemberDataContext.Provider>;
}
