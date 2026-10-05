import { useId, useState } from 'react';
import '../styles/password-input.css';

function EyeIcon({ visible }) {
  return (
    <svg className="password-input__icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M2.2 12s3.5-6 9.8-6 9.8 6 9.8 6-3.5 6-9.8 6-9.8-6-9.8-6Z" />
      <circle cx="12" cy="12" r="2.7" />
      {visible ? <path d="m4 4 16 16" /> : null}
    </svg>
  );
}

export default function PasswordInput({ id, label = 'Password', className = '', ...inputProps }) {
  const generatedId = useId();
  const inputId = id || `password-input-${generatedId}`;
  const [visible, setVisible] = useState(false);
  const fieldName = label.toLowerCase() === 'confirm password' ? 'confirmation' : label.toLowerCase();
  const buttonLabel = `${visible ? 'Hide' : 'Show'} ${fieldName}`;

  return (
    <>
      {label ? <label htmlFor={inputId}>{label}</label> : null}
      <span className={`password-input${className ? ` ${className}` : ''}`}>
        <input
          {...inputProps}
          id={inputId}
          className="password-input__field"
          type={visible ? 'text' : 'password'}
        />
        <button
          type="button"
          className="password-input__toggle"
          aria-label={buttonLabel}
          aria-pressed={visible}
          aria-controls={inputId}
          title={buttonLabel}
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => setVisible((current) => !current)}
        >
          <EyeIcon visible={visible} />
        </button>
      </span>
    </>
  );
}
