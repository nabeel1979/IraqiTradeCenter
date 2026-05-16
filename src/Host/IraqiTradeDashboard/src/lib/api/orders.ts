import { api } from './client';
import type { ApiResponse, IncomingOrderDto, PagedResult, SalesInvoiceDto } from '@/types/api';

export const ordersApi = {
  getPending: async (params: { pageNumber?: number; pageSize?: number; status?: string } = {}) => {
    const res = await api.get<ApiResponse<PagedResult<IncomingOrderDto>>>('/incomingorders/pending', { params });
    return res.data.data!;
  },
  confirm: async (orderId: number, data: { salesRepId: number; taxRate: number; discountPercentage: number }) => {
    const res = await api.post<ApiResponse<SalesInvoiceDto>>(`/incomingorders/${orderId}/confirm`, data);
    return res.data;
  },
};
