import { api } from './client';
import type { ApiResponse, SalesRepDto, SalesRepPerformanceDto } from '@/types/api';

export const salesRepsApi = {
  add: async (data: any) => {
    const res = await api.post<ApiResponse<SalesRepDto>>('/salesreps', data);
    return res.data;
  },
  calculateCommission: async (id: number, from: string, to: string) => {
    const res = await api.post<ApiResponse<number>>(`/salesreps/${id}/calculate-commission`, null, { params: { from, to } });
    return res.data;
  },
  getPerformance: async (id: number, from: string, to: string) => {
    const res = await api.get<ApiResponse<SalesRepPerformanceDto>>(`/salesreps/${id}/performance`, { params: { from, to } });
    return res.data;
  },
};
