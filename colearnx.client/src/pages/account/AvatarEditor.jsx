import { useEffect, useState } from 'react';
import { usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';

export default function AvatarEditor() {
  const { user, token, refreshUser } = useAuth();
  const [image, setImage] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!user?.avatarUrl) return undefined;
    const controller = new AbortController();
    let url;
    usersApi.avatar(user.avatarUrl, token, controller.signal)
      .then((blob) => {
        if (!controller.signal.aborted) {
          url = URL.createObjectURL(blob);
          setImage({ source: user.avatarUrl, url });
        }
      })
      .catch((cause) => { if (!controller.signal.aborted) setError(cause.message); });
    return () => {
      controller.abort();
      if (url) URL.revokeObjectURL(url);
    };
  }, [user?.avatarUrl, token]);

  const initials = (user?.fullName || 'U').split(/\s+/).filter(Boolean).slice(0, 2)
    .map((part) => part[0]).join('').toUpperCase();

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
    <div className="avatar lg" style={{ margin: '0 auto 12px', overflow: 'hidden' }}>
      {image && image.source === user?.avatarUrl ? <img src={image.url} alt="Your avatar" style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : initials}
    </div>
    <label className="btn btn-ghost btn-sm" htmlFor="account-avatar">{busy ? 'Uploading…' : 'Upload avatar'}</label>
    <input id="account-avatar" type="file" accept="image/png,image/jpeg,image/webp" onChange={upload} disabled={busy} style={{ display: 'none' }} />
    {error ? <p role="alert" className="trainer-error">{error}</p> : null}
  </div>;
}
