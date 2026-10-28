import { useContext } from 'react';
import StaffAuthContext from './StaffAuthContext';

export default function useStaffAuth() {
  const context = useContext(StaffAuthContext);
  if (!context) throw new Error('useStaffAuth must be used within StaffAuthProvider');
  return context;
}
