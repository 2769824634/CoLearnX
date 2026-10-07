import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

export function useAccessNotice() {
  const location = useLocation();
  const navigate = useNavigate();
  const initialNotice = location.state?.accessNotice || '';
  const [notice, setNotice] = useState(initialNotice);
  const noticeRef = useRef(initialNotice);
  const originPathRef = useRef(initialNotice ? `${location.pathname}${location.search}${location.hash}` : null);
  const replacedRef = useRef(false);

  useEffect(() => {
    const incomingNotice = location.state?.accessNotice || '';

    if (incomingNotice && incomingNotice !== noticeRef.current) {
      noticeRef.current = incomingNotice;
      originPathRef.current = `${location.pathname}${location.search}${location.hash}`;
      replacedRef.current = false;
      queueMicrotask(() => setNotice(incomingNotice));
      return;
    }

    if (incomingNotice && !replacedRef.current) {
      replacedRef.current = true;
      navigate(`${location.pathname}${location.search}${location.hash}`, { replace: true, state: null });
      return;
    }

    if (!incomingNotice && replacedRef.current) {
      replacedRef.current = false;
      return;
    }

    const currentPath = `${location.pathname}${location.search}${location.hash}`;
    if (!incomingNotice && noticeRef.current && originPathRef.current !== currentPath) {
      noticeRef.current = '';
      originPathRef.current = null;
      queueMicrotask(() => setNotice(''));
    }
  }, [location, navigate, notice]);

  const dismiss = () => {
    noticeRef.current = '';
    originPathRef.current = null;
    setNotice('');
  };

  return { notice, dismiss };
}

export function AccessNotice({ notice, onDismiss }) {
  if (!notice) return null;
  return (
    <div className="callout warn" role="status" aria-label="Access notice">
      <div className="callout-title">Workspace access</div>
      <div>{notice}</div>
      <button type="button" className="btn btn-ghost" onClick={onDismiss}>Dismiss</button>
    </div>
  );
}
