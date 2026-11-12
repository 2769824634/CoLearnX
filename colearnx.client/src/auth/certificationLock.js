// The server independently enforces certification for workspace APIs.
export const isUncertified = (user, role) =>
  (role === 'trainer' || role === 'creator') && user?.certifiedAt === null;

export function isOpenToUncertified(role, pathname) {
  const path = pathname.replace(/\/+$/, '');
  return [`/${role}`, `/${role}/home`, `/${role}/account`, `/${role}/certification`].includes(path);
}
