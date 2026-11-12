import { useCallback, useEffect, useRef, useState } from 'react';
import { courseTrainersApi } from '../../api/courseTrainers';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';
import { CreatorError } from './CreatorUi';

const SEARCH_DELAY_MS = 300;

// Pick one certified Trainer in the course's specialty for an Intake. onInvite(trainerId) sends them the schedule.
export default function InviteTrainerDialog({ courseId, onInvite, busy, error, onClose }) {
  const { token } = useAuth();
  const [term, setTerm] = useState('');
  const [results, setResults] = useState(null);
  const [searchError, setSearchError] = useState(null);
  const timer = useRef(null);
  const pending = useRef(null);

  const search = useCallback((value) => {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    courseTrainersApi.searchTrainers(token, courseId, value, controller.signal).then(
      (items) => {
        if (controller.signal.aborted) return;
        setSearchError(null);
        setResults(Array.isArray(items) ? items : []);
      },
      (requestError) => { if (!controller.signal.aborted) setSearchError(requestError); },
    );
  }, [token, courseId]);

  // The Creator may not know any Trainer by name, so matching trainers are listed as soon as the dialog opens.
  useEffect(() => {
    search('');
    return () => {
      clearTimeout(timer.current);
      pending.current?.abort();
    };
  }, [search]);

  function onChange(event) {
    const value = event.target.value;
    setTerm(value);
    clearTimeout(timer.current);
    pending.current?.abort();
    timer.current = setTimeout(() => search(value.trim()), SEARCH_DELAY_MS);
  }

  return <Modal open title="Invite a trainer" onClose={onClose}>
    <p className="page-sub" style={{ marginTop: 0 }}>The trainer receives every session of this intake and accepts or declines the whole schedule.</p>
    <div className="form-group">
      <label htmlFor="intake-trainer-search">Certified trainers in this specialty</label>
      <input id="intake-trainer-search" type="search" autoComplete="off" value={term} onChange={onChange} placeholder="Filter by name" data-modal-autofocus />
    </div>
    <CreatorError error={error || searchError} />
    {results === null ? (searchError ? null : <p className="page-sub" role="status">Loading trainers…</p>)
      : !results.length ? <p className="page-sub">{term.trim() ? `No trainers match “${term.trim()}”.` : 'No certified trainers in this specialty yet.'}</p>
      : <ul className="later-resource-list">{results.map((candidate) => <li key={candidate.id}>
        <div><strong>{candidate.fullName}</strong><small>{[(candidate.specialties || []).join(', '), candidate.headline, candidate.specialisations].filter(Boolean).join(' · ') || 'No profile details yet'}</small></div>
        <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onInvite(candidate.id)}>Invite</button>
      </li>)}</ul>}
  </Modal>;
}
