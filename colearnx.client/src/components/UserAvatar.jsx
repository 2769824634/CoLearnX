import { useEffect, useState } from 'react';
import { usersApi } from '../api';

function initialsFrom(name) {
  return (name || 'U').split(/\s+/).filter(Boolean).slice(0, 2)
    .map((part) => part[0]).join('').toUpperCase();
}

export default function UserAvatar({ name, avatarUrl, token, size = '' }) {
  const avatarIdentity = avatarUrl && token ? `${avatarUrl}\u0000${token}` : '';
  const [image, setImage] = useState({ identity: '', url: null });

  useEffect(() => {
    if (!avatarIdentity) return undefined;
    const controller = new AbortController();
    let objectUrl;
    usersApi.avatar(avatarUrl, token, controller.signal)
      .then((blob) => {
        if (controller.signal.aborted) return;
        objectUrl = URL.createObjectURL(blob);
        setImage({ identity: avatarIdentity, url: objectUrl });
      })
      .catch(() => {
        if (!controller.signal.aborted) setImage({ identity: avatarIdentity, url: null });
      });
    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [avatarIdentity, avatarUrl, token]);

  const imageUrl = image.identity === avatarIdentity ? image.url : null;

  return (
    <div className={`avatar${size ? ` ${size}` : ''}`}>
      {imageUrl
        ? <img src={imageUrl} alt={name || 'Your avatar'} />
        : initialsFrom(name)}
    </div>
  );
}
