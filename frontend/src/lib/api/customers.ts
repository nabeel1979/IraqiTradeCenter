import { api } from './client';
import type { ApiResponse, CustomerStatementDto } from '@/types/api';

export const customersApi = {
  getStatement: async (id: number, from: string, to: string) => {
    const res = await api.get<ApiResponse<CustomerStatementDto>>(`/customers/${id}/statement`, { params: { from, to } });
    return res.data.data!;
  },
};
