import { api } from './client';
import type {
  ApiResponse,
  CompanyDto,
  CreateCompanyPayload,
  UpdateCompanyPayload,
  PagedResult,
} from '@/types/api';

export const companiesApi = {
  list: async (params: {
    pageNumber?: number;
    pageSize?: number;
    search?: string;
    activeOnly?: boolean;
  } = {}) => {
    const res = await api.get<ApiResponse<PagedResult<CompanyDto>>>('/companies', { params });
    return res.data.data!;
  },

  getById: async (id: number) => {
    const res = await api.get<ApiResponse<CompanyDto>>(`/companies/${id}`);
    return res.data.data!;
  },

  create: async (data: CreateCompanyPayload) => {
    const res = await api.post<ApiResponse<CompanyDto>>('/companies', data);
    return res.data;
  },

  update: async (id: number, data: UpdateCompanyPayload) => {
    const res = await api.put<ApiResponse<CompanyDto>>(`/companies/${id}`, data);
    return res.data;
  },

  toggleStatus: async (id: number) => {
    const res = await api.patch<ApiResponse<boolean>>(`/companies/${id}/toggle-status`);
    return res.data;
  },
};
