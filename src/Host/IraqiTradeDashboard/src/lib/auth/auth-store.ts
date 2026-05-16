import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface User {
  id: string;
  fullName: string;
  phone: string;
  role: string;
}

interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  setUser: (user: User) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    set => ({
      user: null,
      isAuthenticated: false,
      setUser: user => set({ user, isAuthenticated: true }),
      logout: () => {
        localStorage.removeItem('iqtc_token');
        set({ user: null, isAuthenticated: false });
      },
    }),
    { name: 'iqtc_auth' }
  )
);
