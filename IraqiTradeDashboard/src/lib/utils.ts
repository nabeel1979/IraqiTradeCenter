import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** تنسيق العملة العراقية */
export function formatIQD(amount: number | null | undefined, opts: { decimals?: number; symbol?: boolean } = {}) {
  if (amount == null) return '—';
  const { decimals = 0, symbol = true } = opts;
  const formatted = new Intl.NumberFormat('en-US', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(amount);
  return symbol ? `${formatted} د.ع` : formatted;
}

/** تنسيق التاريخ بالعربية */
export function formatDate(d: string | Date | null | undefined, opts: { short?: boolean } = {}) {
  if (!d) return '—';
  const date = typeof d === 'string' ? new Date(d) : d;
  if (isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('ar-IQ', {
    year: 'numeric',
    month: opts.short ? '2-digit' : 'long',
    day: '2-digit',
  }).format(date);
}

export function formatDateTime(d: string | Date | null | undefined) {
  if (!d) return '—';
  const date = typeof d === 'string' ? new Date(d) : d;
  if (isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('ar-IQ', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

/** أحرف اسم → initials */
export function initials(name?: string | null) {
  if (!name) return '—';
  const parts = name.trim().split(/\s+/);
  return parts.slice(0, 2).map(p => p[0]).join('');
}

/** تأخير */
export const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));
