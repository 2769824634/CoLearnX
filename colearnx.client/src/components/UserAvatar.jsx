import { useEffect, useState } from 'react';
import { usersApi } from '../api';

const avatarCache = new Map();

export function clearAvatarCache() {
  for (const url of avatarCache.values()) URL.revokeObjectURL(url);
  avatarCache.clear();
}

function dropOtherAvatars(identity) {
  for (const [key, url] of avatarCache) {
    if (key === identity) continue;
    URL.revokeObjectURL(url);
    avatarCache.delete(key);
  }
}

function initialsFrom(name) {
  return (name || 'U').split(/\s+/).filter(Boolean).slice(0, 2)
    .map((part) => part[0]).join('').toUpperCase();
}

export default function UserAvatar({ name, avatarUrl, token, size = '' }) {
  const avatarIdentity = avatarUrl && token ? `${avatarUrl}\u0000${token}` : '';
  const [, redraw] = useState(0);
  const imageUrl = avatarIdentity ? avatarCache.get(avatarIdentity) : null;

  useEffect(() => {
    if (!avatarIdentity) return undefined;
    dropOtherAvatars(avatarIdentity);
    if (avatarCache.has(avatarIdentity)) return undefined;

    const controller = new AbortController();
    usersApi.avatar(avatarUrl, token, controller.signal)
      .then((blob) => {
        if (controller.signal.aborted) return;
        avatarCache.set(avatarIdentity, URL.createObjectURL(blob));
        redraw((version) => version + 1);
      })
      .catch(() => undefined);
    return () => controller.abort();
  }, [avatarIdentity, avatarUrl, token]);

  return (
    <div className={`avatar${size ? ` ${size}` : ''}`}>
      {imageUrl
        ? <img src={imageUrl} alt={name || 'Your avatar'} />
        : initialsFrom(name)}
    </div>
  );
}
