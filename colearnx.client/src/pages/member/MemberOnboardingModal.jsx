import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Modal from '../../components/Modal';
import { interestsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';

export function needsMemberOnboarding(user) {
  return Boolean(user && 'onboardingCompletedAt' in user && !user.onboardingCompletedAt && !user.onboardingSkippedAt);
}

export default function MemberOnboardingModal({ dismissible = false }) {
  const navigate = useNavigate();
  const { user, refreshUser } = useAuth();
  const [tree, setTree] = useState([]);
  const [step, setStep] = useState(1);
  const [goal, setGoal] = useState('professional');
  const [categories, setCategories] = useState([]);
  const [selected, setSelected] = useState([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    Promise.all([interestsApi.tree(), interestsApi.my()]).then(([items, saved]) => {
      if (!active) return;
      setTree(items);
      setSelected(saved.interestIds || []);
      if (saved.learningGoals === 'hobby' || saved.learningGoals === 'professional') setGoal(saved.learningGoals);
      setCategories(items.filter((item) => item.children.some((leaf) => saved.interestIds?.includes(leaf.id))).map((item) => item.id));
    }).catch((reason) => { if (active) setError(reason.message || 'Could not load interests.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  async function finish(skip) {
    setBusy(true);
    setError('');
    try {
      const saved = await interestsApi.save({ interestIds: skip ? [] : selected, learningGoals: skip ? null : goal, skip });
      refreshUser({
        ...user,
        learningGoals: saved.learningGoals,
        onboardingCompletedAt: saved.onboardingCompletedAt,
        onboardingSkippedAt: saved.onboardingSkippedAt,
      });
      navigate('/', { replace: true });
    } catch (reason) {
      setError(reason.message || 'Could not save your interests.');
    } finally {
      setBusy(false);
    }
  }

  function toggleCategory(id) {
    setCategories((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
    const leaves = tree.find((item) => item.id === id)?.children.map((item) => item.id) || [];
    if (categories.includes(id)) setSelected((current) => current.filter((item) => !leaves.includes(item)));
  }

  function toggleLeaf(id) {
    setSelected((current) => current.includes(id) ? current.filter((item) => item !== id)
      : current.length < 8 ? [...current, id] : current);
  }

  return (
    <Modal
      open
      title="Your learning interests"
      width={760}
      onClose={dismissible ? () => navigate('/', { replace: true }) : undefined}
    >
      <p className="page-sub" style={{ marginTop: 0 }}>Step {step} of 3 · You can change this later in Account.</p>
      {loading ? <p role="status">Loading interests…</p> : null}
      {error ? <p className="callout warn" role="alert">{error}</p> : null}
      {!loading && step === 1 ? (
        <>
          <h2>What brings you here?</h2>
          <p>Choose a learning goal. Recommendations follow your course progress, not this choice.</p>
          <label className="form-group"><input type="radio" name="goal" checked={goal === 'hobby'} onChange={() => setGoal('hobby')} /> Hobby</label>
          <label className="form-group"><input type="radio" name="goal" checked={goal === 'professional'} onChange={() => setGoal('professional')} /> Professional</label>
        </>
      ) : null}
      {!loading && step === 2 ? (
        <>
          <h2>Choose broad areas</h2>
          <p>Choose any areas you want to explore.</p>
          <div className="grid-2">{tree.map((item) => (
            <label key={item.id} className="card card-body">
              <input type="checkbox" checked={categories.includes(item.id)} onChange={() => toggleCategory(item.id)} /> {item.name}
            </label>
          ))}
          </div>
        </>
      ) : null}
      {!loading && step === 3 ? (
        <>
          <h2>Choose specific interests</h2>
          <p>Choose up to 8. Your completed courses become the basis for later recommendations.</p>
          {tree.filter((item) => categories.includes(item.id)).map((category) => (
            <fieldset key={category.id} className="form-group">
              <legend>{category.name}</legend>
              <div className="grid-2">{category.children.map((leaf) => (
                <label key={leaf.id}>
                  <input type="checkbox" checked={selected.includes(leaf.id)} disabled={busy || (!selected.includes(leaf.id) && selected.length >= 8)} onChange={() => toggleLeaf(leaf.id)} /> {leaf.name}
                </label>
              ))}
              </div>
            </fieldset>
          ))}
          {!categories.length ? <p>Select an area in step 2, or Skip for high-rated Beginner courses.</p> : null}
        </>
      ) : null}
      <div className="modal-actions">
        {step > 1 ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setStep(step - 1)}>Back</button> : null}
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => finish(true)}>Skip</button>
        {step < 3 ? (
          <button type="button" className="btn btn-primary" disabled={loading || busy || (step === 2 && !categories.length)} onClick={() => setStep(step + 1)}>Next</button>
        ) : (
          <button type="button" className="btn btn-primary" disabled={busy || !selected.length} onClick={() => finish(false)}>{busy ? 'Saving…' : 'Save interests'}</button>
        )}
      </div>
    </Modal>
  );
}
