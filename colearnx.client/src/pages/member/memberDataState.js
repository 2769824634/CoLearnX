import { createContext, useContext } from 'react';

export const MemberDataContext = createContext(null);

export function useMemberData() {
  const context = useContext(MemberDataContext);
  if (!context) throw new Error('useMemberData must be used within MemberDataProvider');
  return context;
}
