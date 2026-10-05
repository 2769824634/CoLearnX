import { useState } from 'react';

export function useNoCredentialAutofill() {
  const [locked, setLocked] = useState(true);
  function unlock(event) {
    event.currentTarget?.removeAttribute('readonly');
    setLocked(false);
  }
  return {
    autoComplete: 'off',
    readOnly: locked,
    onFocus: unlock,
    onClick: unlock,
    onPointerDown: unlock,
    'data-lpignore': 'true',
    'data-1p-ignore': '',
  };
}
