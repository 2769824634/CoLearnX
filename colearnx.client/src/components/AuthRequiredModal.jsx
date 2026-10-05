import { Link } from 'react-router-dom';
import Modal from './Modal';

export default function AuthRequiredModal({ open, onClose }) {
  return (
    <Modal open={open} title="Sign in to continue" onClose={onClose} width={440}>
      <p>Create an account or sign in to enrol, save a wishlist, top up credits, or open your member pages.</p>
      <div className="modal-actions">
        <Link className="btn btn-ghost" to="/login" onClick={onClose}>Log in</Link>
        <Link className="btn btn-primary" to="/register" onClick={onClose}>Sign up</Link>
      </div>
    </Modal>
  );
}
