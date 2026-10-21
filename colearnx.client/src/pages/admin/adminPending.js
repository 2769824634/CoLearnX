import { createContext, useContext } from 'react';

export const PENDING_REFRESH_MS = 30_000;

export const AdminPendingContext = createContext(null);

export function approvalTotal(counts) {
  if (!counts) return 0;
  return counts.roleRequests + counts.courses + counts.materialVersions + counts.certificates;
}

export function pendingTotal(counts) {
  return approvalTotal(counts) + (counts?.disputes ?? 0);
}

export function useAdminPending() {
  return useContext(AdminPendingContext);
}
