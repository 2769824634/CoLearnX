import { useEffect, useState } from 'react';
import { usersApi } from '../api';

function initialsFrom(name) {
  return (name || 'U').split(/\s+/).filter(Boolean).slice(0, 2)
    .map((part) => part[0]).join('').toUpperCase();
}

export default function UserAvatar({ name, avatarUrl, token, size = '' }) {
  const [imageUrl, setImageUrl] = useState(null);

  useEffect(() => {
    if (!avatarUrl || !token) {
      setImageUrl(null);
      return undefined;
    }
    const controller = new AbortController();
    let objectUrl;
    usersApi.avatar(avatarUrl, token, controller.signal)
      .then((blob) => {
        if (controller.signal.aborted) return;
        objectUrl = URL.createObjectURL(blob);
        setImageUrl(objectUrl);
      })
      .catch(() => {
        if (!controller.signal.aborted) setImageUrl(null);
      });
    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [avatarUrl, token]);

  return (
    <div className={`avatar${size ? ` ${size}` : ''}`}>
      {imageUrl
        ? <img src={imageUrl} alt={name || 'Your avatar'} />
        : initialsFrom(name)}
    </div>
  );
}
