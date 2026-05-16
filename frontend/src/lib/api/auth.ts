import { api } from './client';
import type { LoginRequest, LoginResponse, ApiResponse } from '@/types/api';

export const authApi = {
  login: async (data: LoginRequest) => {
    const res = await api.post<ApiResponse<LoginResponse>>('/auth/login', data);
    return res.data;
  },
};
