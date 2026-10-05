import { useEffect, useId, useRef } from 'react';

const FOCUSABLE = [
  'a[href]',
  'area[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[contenteditable="true"]',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

function focusableElements(panel) {
  return panel ? Array.from(panel.querySelectorAll(FOCUSABLE)).filter((element) => element.getAttribute('aria-hidden') !== 'true') : [];
}

function isTopmost(panel) {
  const dialogs = Array.from(document.querySelectorAll('.modal[role="dialog"]'));
  return dialogs[dialogs.length - 1] === panel;
}

export default function Modal({ open, title, onClose, children, width }) {
  const isOpen = Boolean(open);
  const dialogRef = useRef(null);
  const previousFocusRef = useRef(null);
  const onCloseRef = useRef(onClose);
  const titleId = useId();
  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!isOpen) return undefined;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const panel = dialogRef.current;
    const controls = focusableElements(panel);
    const preferred = panel?.querySelector('[data-modal-autofocus]')
      || controls.find((element) => !element.classList.contains('modal-close'))
      || controls[0]
      || panel;
    preferred?.focus();

    function onKeyDown(event) {
      if (!isTopmost(panel)) return;
      if (event.key === 'Escape') {
        event.preventDefault();
        onCloseRef.current?.();
        return;
      }
      if (event.key !== 'Tab') return;
      const current = focusableElements(panel);
      if (current.length === 0) {
        event.preventDefault();
        panel?.focus();
        return;
      }
      const first = current[0];
      const last = current[current.length - 1];
      if (!panel.contains(document.activeElement)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      const previous = previousFocusRef.current;
      if (previous && document.contains(previous)) previous.focus();
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div
      className="modal-overlay"
      onClick={(event) => {
        if (event.target === event.currentTarget && isTopmost(dialogRef.current)) onCloseRef.current?.();
      }}
    >
      <div
        ref={dialogRef}
        className="modal"
        style={width ? { width } : undefined}
        role="dialog"
        aria-modal="true"
        aria-labelledby={title ? titleId : undefined}
        tabIndex={-1}
      >
        <div className="modal-header">
          <h2 id={title ? titleId : undefined} style={{ margin: 0, font: 'inherit' }}>{title}</h2>
          {onClose ? (
            <button type="button" className="modal-close" onClick={() => onCloseRef.current?.()} aria-label="Close">
              ✕
            </button>
          ) : null}
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}
