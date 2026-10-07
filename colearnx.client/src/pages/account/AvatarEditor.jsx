import { useState } from 'react';
import { usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import UserAvatar from '../../components/UserAvatar';

export default function AvatarEditor({ role }) {
  const { user, token, refreshUser } = useAuth();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function upload(event) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 2 * 1024 * 1024) {
      setError('Choose a PNG, JPEG or WebP image up to 2 MB.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      refreshUser(await usersApi.uploadAvatar(user.id, file, token));
    } catch (cause) {
      setError(cause.message || 'Avatar upload failed.');
    } finally {
      setBusy(false);
    }
  }

  return <div>
    <div className="avatar-frame" data-role={role} style={{ margin: '0 auto 12px', width: 'fit-content' }}>
      <UserAvatar name={user?.fullName} avatarUrl={user?.avatarUrl} token={token} size="lg" />
    </div>
    <label className="btn btn-ghost btn-sm" htmlFor="account-avatar">{busy ? 'Uploading…' : 'Upload avatar'}</label>
    <input id="account-avatar" type="file" accept="image/png,image/jpeg,image/webp" onChange={upload} disabled={busy} style={{ display: 'none' }} />
    <p style={{ fontSize: 11, color: 'var(--slate)', margin: '8px 0 0' }}>Accepted formats: PNG, JPEG or WebP. Maximum size: 2 MB.</p>
    {error ? <p role="alert" className="trainer-error">{error}</p> : null}
  </div>;
}
