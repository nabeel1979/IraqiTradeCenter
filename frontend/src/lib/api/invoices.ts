import { api } from './client';
import type { ApiResponse, SalesInvoiceDto } from '@/types/api';

export interface CreateInvoicePayload {
  customerId: number; salesRepId?: number; incomingOrderId?: number;
  taxRate: number; discountPercentage: number; discountAmount: number;
  notes?: string;
  lines: Array<{ itemId: number; unitOfMeasureId: number; quantity: number; unitPriceOverride?: number; lineDiscount: number; }>;
}

export interface RecordPaymentPayload {
  amount: number; paymentMethod: string; referenceNumber?: string; notes?: string;
}

export const invoicesApi = {
  create: async (data: CreateInvoicePayload) => {
    const res = await api.post<ApiResponse<SalesInvoiceDto>>('/salesinvoices', data);
    return res.data;
  },
  recordPayment: async (invoiceId: number, data: RecordPaymentPayload) => {
    const res = await api.post<ApiResponse<number>>(`/salesinvoices/${invoiceId}/payments`, data);
    return res.data;
  },
};
