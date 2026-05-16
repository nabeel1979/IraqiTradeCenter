import { api } from './client';
import type { LoginRequest, LoginResponse, ApiResponse } from '@/types/api';

const LOCAL_ADMIN_PASSWORD = 'Admin@2026';
const LOCAL_TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZnVsbE5hbWUiOiLYp9mE2YXYr9mK2LEg2KfZhNi52KfZhSIsInBob25lIjoiMDc3MDAwMDAwMDAiLCJyb2xlIjoiQWRtaW4iLCJpc3MiOiJJcmFxaVRyYWRlQ2VudGVyIiwiYXVkIjoiSXJhcWlUcmFkZUNlbnRlckNvbXBhbnkiLCJpYXQiOjE3Nzg5MDI3OTMsImV4cCI6MTc3OTk4OTE5M30.placeholder';

export const authApi = {
  login: async (data: LoginRequest): Promise<ApiResponse<LoginResponse>> => {
    try {
      const res = await api.post<ApiResponse<LoginResponse>>('/auth/login', data);
      return res.data;
    } catch {
      // fallback محلي مؤقت ريثما يُبنى Auth endpoint في الـ Backend
      if (data.password === LOCAL_ADMIN_PASSWORD) {
        return {
          success: true,
          data: {
            token: LOCAL_TOKEN,
            expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
            user: {
              id: '1',
              fullName: 'المدير العام',
              phone: data.phone,
              role: 'Admin',
            },
          },
        };
      }
      return { success: false, errors: ['رقم الهاتف أو كلمة المرور غير صحيحة'] };
    }
  },
};
