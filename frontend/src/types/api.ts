// ════════════════════════════════════════
// API Types - matching backend DTOs
// ════════════════════════════════════════

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

// ── Authentication
export interface LoginRequest {
  phone: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: {
    id: string;
    fullName: string;
    phone: string;
    role: string;
  };
}

// ── Inventory
export interface ItemDto {
  id: number;
  code: string;
  barcode: string;
  nameAr: string;
  purchasePrice: number;
  baseSalesPrice: number;
  stockBaseQuantity: number;
  minimumStockLevel: number;
  isAvailableForSale: boolean;
  isLowStock: boolean;
}

export interface ItemSnapshot {
  id: number;
  code: string;
  nameAr: string;
  baseUnitId: number;
  baseUnitName: string;
  mediumUnitId?: number;
  mediumUnitName?: string;
  mediumUnitFactor?: number;
  largeUnitId?: number;
  largeUnitName?: string;
  largeUnitFactor?: number;
  baseSalesPrice: number;
  mediumSalesPrice?: number;
  largeSalesPrice?: number;
  availableStock: number;
  isAvailableForSale: boolean;
}

// ── Sales / Store
export interface SalesInvoiceDto {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  customerId: number;
  customerName?: string;
  salesRepId?: number;
  status: 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Cancelled';
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  journalEntryId?: number;
  lines: SalesInvoiceLineDto[];
}

export interface SalesInvoiceLineDto {
  id: number;
  itemId: number;
  itemName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineDiscount: number;
  lineTotal: number;
}

export interface SalesRepDto {
  id: number;
  employeeCode: string;
  fullName: string;
  phone: string;
  commissionType: 'Fixed' | 'Tiered';
  fixedCommissionRate?: number;
  baseSalary: number;
  region?: string;
}

export interface SalesRepPerformanceDto {
  salesRepId: number;
  fullName: string;
  fromDate: string;
  toDate: string;
  totalSales: number;
  invoiceCount: number;
  calculatedCommission: number;
}

export interface CustomerStatementDto {
  customerId: number;
  customerName: string;
  fromDate: string;
  toDate: string;
  openingBalance: number;
  closingBalance: number;
  lines: CustomerStatementLineDto[];
}

export interface CustomerStatementLineDto {
  date: string;
  docType: string;
  docNumber: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface IncomingOrderDto {
  id: number;
  platformOrderId: string;
  platformOrderNumber: string;
  receivedAt: string;
  customerId: number;
  customerName?: string;
  status: 'Pending' | 'Reviewed' | 'Confirmed' | 'Rejected';
  totalAmount: number;
  assignedSalesRepId?: number;
  createdInvoiceId?: number;
  items: IncomingOrderItemDto[];
}

export interface IncomingOrderItemDto {
  id: number;
  itemId: number;
  itemName: string;
  unitOfMeasureId: number;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

// ── Accounting
export interface AccountDto {
  id: number;
  code: string;
  nameAr: string;
  type: number;
  nature: number;
  parentId?: number;
  level: number;
  isLeaf: boolean;
  openingBalance: number;
  children: AccountDto[];
}

export interface TrialBalanceRowDto {
  accountId: number;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  balance: number;
}
