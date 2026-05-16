import { api } from './client';
import type { ApiResponse, AccountDto, TrialBalanceRowDto } from '@/types/api';

export const accountingApi = {
  getTree: async () => {
    const res = await api.get<ApiResponse<AccountDto[]>>('/accounts/tree');
    return res.data.data ?? [];
  },
  getTrialBalance: async (from: string, to: string) => {
    const res = await api.get<ApiResponse<TrialBalanceRowDto[]>>('/accounts/trial-balance', { params: { from, to } });
    return res.data.data ?? [];
  },
  postJournalEntry: async (data: any) => {
    const res = await api.post<ApiResponse<number>>('/accounts/journal-entries', data);
    return res.data;
  },
};
