import { Link } from 'react-router-dom';
import Modal from './Modal';

export default function CertificationRequiredModal({ open, role, onClose }) {
  return (
    <Modal open={open} title="Certification required" onClose={onClose} width={460}>
      <p>This part of the {role} workspace opens after an administrator approves your certification. Until then you can edit your profile.</p>
      <div className="modal-actions">
        <Link className="btn btn-primary" to={`/${role}/certification`} onClick={onClose}>Start certification</Link>
        <Link className="btn btn-ghost" to={`/${role}/account`} onClick={onClose}>Edit my profile</Link>
      </div>
    </Modal>
  );
}
