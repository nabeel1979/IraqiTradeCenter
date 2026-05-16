import { Plus, Receipt, Search, FileText } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { EmptyState } from '@/components/shared/EmptyState';
import { formatIQD, formatDate } from '@/lib/utils';

// عينة بيانات - في الإنتاج تُجلب من API
const sampleInvoices = [
  { id: 1, invoiceNumber: 'INV-20250513-A8F2', invoiceDate: '2025-05-13', customerName: 'متجر الأمل', totalAmount: 1250000, paidAmount: 1250000, status: 'Paid' },
  { id: 2, invoiceNumber: 'INV-20250513-B3D9', invoiceDate: '2025-05-13', customerName: 'بقالة الكرخ', totalAmount: 875000, paidAmount: 500000, status: 'PartiallyPaid' },
  { id: 3, invoiceNumber: 'INV-20250512-C7E1', invoiceDate: '2025-05-12', customerName: 'سوبر ماركت بغداد', totalAmount: 2400000, paidAmount: 0, status: 'Issued' },
  { id: 4, invoiceNumber: 'INV-20250512-D2F4', invoiceDate: '2025-05-12', customerName: 'متجر الزهور', totalAmount: 540000, paidAmount: 540000, status: 'Paid' },
];

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; variant: any }> = {
    Paid: { label: 'مدفوعة', variant: 'success' },
    PartiallyPaid: { label: 'جزئياً', variant: 'warning' },
    Issued: { label: 'مصدرة', variant: 'default' },
    Draft: { label: 'مسودة', variant: 'muted' },
    Cancelled: { label: 'ملغاة', variant: 'destructive' },
  };
  const cfg = map[status] ?? { label: status, variant: 'muted' };
  return <Badge variant={cfg.variant}>{cfg.label}</Badge>;
}

export function InvoicesListPage() {
  if (!sampleInvoices.length) {
    return (
      <EmptyState
        icon={Receipt}
        title="لا توجد فواتير"
        description="ابدأ بإنشاء أول فاتورة مبيعات"
        action={
          <Link to="/invoices/new">
            <Button><Plus className="h-4 w-4" />فاتورة جديدة</Button>
          </Link>
        }
      />
    );
  }

  return (
    <div className="space-y-5">
      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <div className="relative flex-1 min-w-[260px]">
            <Search className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input placeholder="رقم فاتورة، اسم عميل..." className="pr-10" />
          </div>
          <select className="h-10 rounded-md border border-input bg-secondary/40 px-3 text-sm">
            <option>كل الحالات</option>
            <option>مدفوعة</option>
            <option>جزئياً</option>
            <option>مصدرة</option>
            <option>ملغاة</option>
          </select>
          <Link to="/invoices/new" className="mr-auto">
            <Button>
              <Plus className="h-4 w-4" />
              فاتورة جديدة
            </Button>
          </Link>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          <table className="data-table">
            <thead>
              <tr>
                <th>رقم الفاتورة</th>
                <th>التاريخ</th>
                <th>العميل</th>
                <th className="text-left">الإجمالي</th>
                <th className="text-left">المدفوع</th>
                <th className="text-left">المتبقي</th>
                <th>الحالة</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {sampleInvoices.map(inv => (
                <tr key={inv.id}>
                  <td>
                    <span className="num-display text-xs">{inv.invoiceNumber}</span>
                  </td>
                  <td className="text-sm text-muted-foreground">{formatDate(inv.invoiceDate)}</td>
                  <td className="font-medium">{inv.customerName}</td>
                  <td className="text-left num-display">{formatIQD(inv.totalAmount)}</td>
                  <td className="text-left num-display text-success">{formatIQD(inv.paidAmount)}</td>
                  <td className="text-left num-display text-muted-foreground">
                    {formatIQD(inv.totalAmount - inv.paidAmount)}
                  </td>
                  <td><StatusBadge status={inv.status} /></td>
                  <td>
                    <button className="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground">
                      <FileText className="h-4 w-4" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardContent>
      </Card>
    </div>
  );
}
