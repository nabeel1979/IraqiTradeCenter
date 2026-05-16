import axios, { AxiosError, AxiosInstance } from 'axios';
import { toast } from 'sonner';

const TOKEN_KEY = 'iqtc_token';

// ════════════════════════════════════════
// API Base URL - من Environment Variables
// ════════════════════════════════════════
// في dev: يستخدم proxy تلقائياً (/api → localhost:6000)
// في build: يستخدم VITE_API_URL مباشرة
const API_BASE_URL = import.meta.env.DEV 
  ? '/api'
  : `${import.meta.env.VITE_API_URL}/api`;

export const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

// طلب: أضف Token تلقائياً
api.interceptors.request.use(config => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// رد: معالجة أخطاء عامة
api.interceptors.response.use(
  res => res,
  (error: AxiosError<{ errors?: string[] }>) => {
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY);
      if (window.location.pathname !== '/login') {
        toast.error('انتهت الجلسة، يرجى تسجيل الدخول');
        setTimeout(() => (window.location.href = '/login'), 1500);
      }
    } else if (error.response?.data?.errors?.length) {
      error.response.data.errors.forEach(e => toast.error(e));
    } else if (error.code === 'ECONNABORTED') {
      toast.error('انتهت مهلة الاتصال بالخادم');
    } else if (!error.response) {
      toast.error('لا يمكن الاتصال بالخادم');
    }
    return Promise.reject(error);
  }
);

export const getToken = () => localStorage.getItem(TOKEN_KEY);
export const setToken = (t: string) => localStorage.setItem(TOKEN_KEY, t);
export const clearToken = () => localStorage.removeItem(TOKEN_KEY);
