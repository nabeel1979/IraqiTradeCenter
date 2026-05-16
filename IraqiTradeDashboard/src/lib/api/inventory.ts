import { api } from './client';
import type { ApiResponse, ItemDto, PagedResult } from '@/types/api';

export interface CreateItemPayload {
  code: string; barcode: string; nameAr: string; categoryId?: number;
  baseUnitId: number; purchasePrice: number; baseSalesPrice: number;
  mediumUnitId?: number; mediumUnitFactor?: number; mediumSalesPrice?: number;
  largeUnitId?: number; largeUnitFactor?: number; largeSalesPrice?: number;
  minimumStockLevel: number; openingStock: number;
}

export const inventoryApi = {
  list: async (params: { pageNumber?: number; pageSize?: number; search?: string; lowStock?: boolean } = {}) => {
    const res = await api.get<ApiResponse<PagedResult<ItemDto>>>('/items', { params });
    return res.data.data!;
  },
  create: async (data: CreateItemPayload) => {
    const res = await api.post<ApiResponse<ItemDto>>('/items', data);
    return res.data;
  },
  recordMovement: async (data: any) => {
    const res = await api.post<ApiResponse<number>>('/items/stock-movements', data);
    return res.data;
  },
};
