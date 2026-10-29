import React from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import Modal from './Modal';

afterEach(() => cleanup());

it('places the overlay outside animated or clipped content so it can cover the entire viewport', () => {
  const { container } = render(<main style={{ transform: 'translateY(0)', overflow: 'hidden' }}>
    <Modal open title="Full page dialog"><p>Dialog content</p></Modal>
  </main>);
  const overlay = screen.getByRole('dialog', { name: 'Full page dialog' }).parentElement;
  expect(overlay.parentElement).toBe(document.body);
  expect(container.contains(overlay)).toBe(false);
});

it('only dismisses the topmost dialog and keeps the lower dialog available', () => {
  const closeOuter = vi.fn();
  const closeInner = vi.fn();
  const { rerender } = render(<Modal open title="Outer" onClose={closeOuter}>
    <Modal open title="Inner" onClose={closeInner}><input aria-label="Inner field" /></Modal>
  </Modal>);
  fireEvent.keyDown(screen.getByLabelText('Inner field'), { key: 'Escape' });
  expect(closeInner).toHaveBeenCalledOnce();
  expect(closeOuter).not.toHaveBeenCalled();
  rerender(<Modal open title="Outer" onClose={closeOuter}><button>Outer action</button></Modal>);
  fireEvent.click(screen.getByRole('dialog', { name: 'Outer' }).parentElement);
  expect(closeOuter).toHaveBeenCalledOnce();
});

it('sets dialog semantics, focuses the first control, traps Tab, and restores focus on close', () => {
  const onClose = vi.fn();
  function Harness() {
    return (
      <>
        <button type="button">Open</button>
        <Modal open title="Edit profile" onClose={onClose}>
          <label htmlFor="first-field">Name</label>
          <input id="first-field" />
          <button type="button">Save</button>
        </Modal>
      </>
    );
  }

  render(<Harness />);
  expect(screen.getByRole('dialog', { name: 'Edit profile' }).getAttribute('aria-modal')).toBe('true');
  expect(document.activeElement).toBe(screen.getByLabelText('Name'));

  const save = screen.getByRole('button', { name: 'Save' });
  save.focus();
  fireEvent.keyDown(save, { key: 'Tab' });
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Close' }));

  const close = screen.getByRole('button', { name: 'Close' });
  fireEvent.keyDown(close, { key: 'Escape' });
  expect(onClose).toHaveBeenCalledTimes(1);
});

it('closes on Escape and restores the trigger focus after the modal unmounts', () => {
  function Harness() {
    const [open, setOpen] = React.useState(false);
    return (
      <>
        <button type="button" onClick={() => setOpen(true)}>Open</button>
        <Modal open={open} title="Dialog" onClose={() => setOpen(false)}>
          <input aria-label="Dialog field" />
        </Modal>
      </>
    );
  }

  render(<Harness />);
  const trigger = screen.getByRole('button', { name: 'Open' });
  trigger.focus();
  fireEvent.click(trigger);
  const dialogField = screen.getByLabelText('Dialog field');
  dialogField.focus();
  fireEvent.keyDown(dialogField, { key: 'Escape' });
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Open' }));
});

it('keeps the current field and caret while an open form draft changes', () => {
  function Harness() {
    const [draft, setDraft] = React.useState(null);
    return (
      <>
        <button type="button" onClick={() => setDraft({ name: '', bio: '' })}>Edit</button>
        <Modal open={draft} title="Profile" onClose={() => setDraft(null)}>
          {draft && <>
            <input aria-label="Name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
            <input aria-label="Bio" value={draft.bio} onChange={(event) => setDraft({ ...draft, bio: event.target.value })} />
          </>}
        </Modal>
      </>
    );
  }

  render(<Harness />);
  const trigger = screen.getByRole('button', { name: 'Edit' });
  trigger.focus();
  fireEvent.click(trigger);
  const bio = screen.getByLabelText('Bio');
  bio.focus();
  for (const value of ['L', 'Le', 'Learning']) {
    fireEvent.change(bio, { target: { value, selectionStart: value.length, selectionEnd: value.length } });
    expect(document.activeElement).toBe(bio);
    expect(bio.selectionStart).toBe(value.length);
  }
  fireEvent.keyDown(bio, { key: 'Escape' });
  expect(document.activeElement).toBe(trigger);
});
