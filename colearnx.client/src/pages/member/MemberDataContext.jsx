import { useCallback, useEffect, useMemo, useState } from 'react';
import { certificatesApi, coursesApi, creditsApi, enrollmentsApi, usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import { CERT_STAGES, initialMemberState } from '../../data/memberMock';
import { MemberDataContext } from './memberDataState';

// Loads member catalog / enrollments / credits from API.
function mapCourseListItem(c) {
  return {
    id: c.id,
    code: c.code,
    title: c.title,
    trainer: c.trainerName,
    credits: c.creditCost,
    level: c.level,
    topic: c.category,
    category: c.category,
    featured: c.isFeatured,
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
    credits: c.creditCost,
    level: c.level,
    topic: c.category,
    category: c.category,
    description: c.description,
    outcomes: c.learningOutcomes || [],
    inWishlist: c.inWishlist,
    alreadyEnrolled: c.alreadyEnrolled,
    sessions: (c.sessions || []).map((s) => ({
      id: s.id,
      label: s.label,
      when: `${new Date(s.startsAt).toLocaleString()} – ${new Date(s.endsAt).toLocaleTimeString()}`,
      seats: s.seatsLeft,
      startsAt: s.startsAt,
      endsAt: s.endsAt,
    })),
  };
}

export function MemberDataProvider({ children }) {
  const { user, refreshUser } = useAuth();
  const [courses, setCourses] = useState([]);
  const [enrolled, setEnrolled] = useState([]);
  const [packages, setPackages] = useState([]);
  const [ledger, setLedger] = useState([]);
  const [certificates, setCertificates] = useState([]);
  const [wishlist, setWishlist] = useState([]);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState('');

  const showToast = useCallback((msg) => setToast(msg), []);

  useEffect(() => {
    if (!toast) return undefined;
    const t = setTimeout(() => setToast(''), 2200);
    return () => clearTimeout(t);
  }, [toast]);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const [courseList, myEnrol, pkgs, myLedger, certs] = await Promise.all([
        coursesApi.list(),
        enrollmentsApi.my(),
        creditsApi.packages(),
        creditsApi.myLedger(),
        certificatesApi.my(),
      ]);
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
          date: new Date(l.createdAt).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
          }),
          type: l.type,
          desc: l.description,
          delta: l.delta,
          balance: l.balanceAfter,
        })),
      );
      setCertificates(certs);
    } catch (e) {
      showToast(e.message || 'Failed to load member data');
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
      user: {
        id: user?.id,
        fullName: user?.fullName ?? '',
        displayName: user?.displayName ?? '',
        email: user?.email ?? '',
        phone: user?.phone ?? '',
        bio: user?.bio ?? '',
        learningGoals: initialMemberState.user.learningGoals,
        specialisations: initialMemberState.user.specialisations,
        trainerHeadline: initialMemberState.user.trainerHeadline,
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
      certStages: CERT_STAGES,
      loading,
    }),
    [user, enrolled, wishlist, ledger, courses, packages, certificates, loading],
  );

  async function loadCourseDetail(id) {
    const detail = await coursesApi.get(id);
    return mapCourseDetail(detail);
  }

  async function enrol(courseId, sessionId) {
    const result = await enrollmentsApi.enrol(courseId, sessionId);
    refreshUser({ ...user, creditBalance: result.balanceAfter });
    await reload();
    return { ok: true, balance: result.balanceAfter, creditsSpent: result.creditsSpent };
  }

  async function topUp(packageId) {
    const row = await creditsApi.topUp(packageId);
    refreshUser({ ...user, creditBalance: row.balanceAfter });
    await reload();
    showToast(`+${row.delta} credits added (simulated)`);
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
      showToast(e.message || 'Wishlist update failed');
    }
  }

  const value = {
    state,
    toast,
    showToast,
    reload,
    loadCourseDetail,
    enrol,
    topUp,
    applyLedgerTopUp,
    saveProfile,
    toggleWish,
  };

  return <MemberDataContext.Provider value={value}>{children}</MemberDataContext.Provider>;
}
