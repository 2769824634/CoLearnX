import { useState } from 'react';

export default function PasswordInput({ id, value, onChange, autoComplete = 'current-password', required = true }) {
  const [visible, setVisible] = useState(false);
  const [capsLock, setCapsLock] = useState(false);
  const capsHintId = `${id}-caps`;
  const trackCapsLock = (event) => setCapsLock(Boolean(event.getModifierState?.('CapsLock')));

  return (
    <>
      <div className="password-input">
        <input
          id={id}
          type={visible ? 'text' : 'password'}
          autoComplete={autoComplete}
          value={value}
          onChange={onChange}
          onKeyDown={trackCapsLock}
          onKeyUp={trackCapsLock}
          onBlur={() => setCapsLock(false)}
          aria-describedby={capsLock ? capsHintId : undefined}
          required={required}
        />
        <button
          type="button"
          className="password-toggle"
          aria-label={visible ? 'Hide password' : 'Show password'}
          aria-pressed={visible}
          aria-controls={id}
          onClick={() => setVisible((current) => !current)}
        >
          {visible ? 'Hide' : 'Show'}
        </button>
      </div>
      {capsLock ? <p id={capsHintId} className="password-caps" role="status">Caps Lock is on</p> : null}
    </>
  );
}
