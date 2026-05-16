import {
  Receipt, Package, Users, Wallet, ArrowUpRight, AlertTriangle, ShoppingBag
} from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer
} from 'recharts';
import { StatCard } from '@/components/shared/StatCard';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { formatIQD } from '@/lib/utils';

// بيانات تجريبية - في الإنتاج تُجلب من الـ API
const salesData = [
  { day: 'سبت', sales: 4200000, orders: 12 },
  { day: 'أحد', sales: 3850000, orders: 9 },
  { day: 'إثنين', sales: 5100000, orders: 14 },
  { day: 'ثلاثاء', sales: 6300000, orders: 18 },
  { day: 'أربعاء', sales: 4900000, orders: 13 },
  { day: 'خميس', sales: 7200000, orders: 21 },
  { day: 'جمعة', sales: 5800000, orders: 16 },
];

const topRepsData = [
  { name: 'أحمد ا.', sales: 18500000 },
  { name: 'محمد ر.', sales: 14200000 },
  { name: 'علي ك.', sales: 11800000 },
  { name: 'حسين م.', sales: 9300000 },
];

const recentInvoices = [
  { id: 'INV-20250513-A8F2', customer: 'متجر الأمل', amount: 1250000, status: 'Paid' },
  { id: 'INV-20250513-B3D9', customer: 'بقالة الكرخ', amount: 875000, status: 'PartiallyPaid' },
  { id: 'INV-20250513-C7E1', customer: 'سوبر ماركت بغداد', amount: 2400000, status: 'Issued' },
  { id: 'INV-20250513-D2F4', customer: 'متجر الزهور', amount: 540000, status: 'Paid' },
];

const lowStockItems = [
  { name: 'حليب الصافي 1 لتر', remaining: 12, unit: 'كرتون' },
  { name: 'زيت الرفيدين 5 لتر', remaining: 3, unit: 'كرتون' },
  { name: 'سكر أبيض 1 كغ', remaining: 28, unit: 'كيس' },
];

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; variant: 'success' | 'warning' | 'default' | 'muted' }> = {
    Paid: { label: 'مدفوعة', variant: 'success' },
    PartiallyPaid: { label: 'مدفوعة جزئياً', variant: 'warning' },
    Issued: { label: 'مصدرة', variant: 'default' },
    Draft: { label: 'مسودة', variant: 'muted' },
  };
  const cfg = map[status] ?? { label: status, variant: 'muted' as const };
  return <Badge variant={cfg.variant}>{cfg.label}</Badge>;
}

export function DashboardPage() {
  const todayDate = new Intl.DateTimeFormat('ar-IQ', {
    weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
  }).format(new Date());

  return (
    <div className="space-y-6">
      {/* Hero strip */}
      <div className="relative overflow-hidden rounded-xl border border-primary/20 bg-gradient-to-br from-primary/[0.08] via-card to-card p-6">
        <div className="pattern-meso absolute inset-0 opacity-40" />
        <div className="relative flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.18em] text-primary/70">{todayDate}</p>
            <h1 className="mt-2 font-display text-3xl font-medium">صباح الخير 👋</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              مبيعات اليوم: <span className="font-medium tnum text-foreground">{formatIQD(5800000)}</span>
              {' · '}
              <span className="text-success">+18% عن الأمس</span>
            </p>
          </div>
          <Link to="/invoices/new">
            <Button size="lg" className="glow-primary">
              <Receipt className="h-4 w-4" />
              فاتورة جديدة
              <ArrowUpRight className="h-4 w-4" />
            </Button>
          </Link>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="مبيعات الشهر"
          value={formatIQD(142500000)}
          icon={Receipt}
          change={{ value: 23, positive: true }}
          hint="مقابل الشهر الماضي"
          variant="primary"
        />
        <StatCard
          label="عدد الفواتير"
          value="342"
          icon={ShoppingBag}
          change={{ value: 12, positive: true }}
          hint="هذا الشهر"
        />
        <StatCard
          label="عملاء نشطون"
          value="68"
          icon={Users}
          change={{ value: 5, positive: true }}
          hint="من أصل 84"
        />
        <StatCard
          label="ذمم العملاء"
          value={formatIQD(28400000)}
          icon={Wallet}
          hint="إجمالي غير مسدّد"
        />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Sales chart */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>المبيعات الأسبوعية</CardTitle>
                <CardDescription>آخر 7 أيام</CardDescription>
              </div>
              <div className="flex items-center gap-2 text-xs">
                <span className="flex items-center gap-1.5">
                  <span className="h-2 w-2 rounded-full bg-primary" />
                  المبيعات
                </span>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            <div className="h-72">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={salesData} margin={{ top: 10, right: 0, left: 0, bottom: 0 }}>
                  <defs>
                    <linearGradient id="salesGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="hsl(35 50% 65%)" stopOpacity={0.4} />
                      <stop offset="100%" stopColor="hsl(35 50% 65%)" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(240 5% 17%)" vertical={false} />
                  <XAxis dataKey="day" stroke="hsl(30 5% 58%)" fontSize={12} reversed tickLine={false} axisLine={false} />
                  <YAxis stroke="hsl(30 5% 58%)" fontSize={12} tickLine={false} axisLine={false}
                    tickFormatter={(v) => `${(v / 1000000).toFixed(1)}M`}
                    orientation="right"
                  />
                  <Tooltip
                    contentStyle={{
                      background: 'hsl(240 5% 10%)',
                      border: '1px solid hsl(240 5% 17%)',
                      borderRadius: 8,
                      fontFamily: '"IBM Plex Sans Arabic", sans-serif',
                    }}
                    formatter={(v: number) => [formatIQD(v), 'المبيعات']}
                    cursor={{ fill: 'hsl(35 50% 65% / 0.05)' }}
                  />
                  <Area type="monotone" dataKey="sales" stroke="hsl(35 50% 65%)" strokeWidth={2} fill="url(#salesGradient)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        {/* Top reps */}
        <Card>
          <CardHeader>
            <CardTitle>أفضل المندوبين</CardTitle>
            <CardDescription>هذا الشهر</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {topRepsData.map((rep, i) => {
                const pct = (rep.sales / topRepsData[0].sales) * 100;
                return (
                  <div key={rep.name} className="space-y-1.5">
                    <div className="flex items-center justify-between text-sm">
                      <div className="flex items-center gap-2">
                        <span className={
                          'flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium ' +
                          (i === 0 ? 'bg-primary/20 text-primary ring-1 ring-primary/40' : 'bg-secondary text-muted-foreground')
                        }>
                          {i + 1}
                        </span>
                        <span className="font-medium">{rep.name}</span>
                      </div>
                      <span className="num-display text-muted-foreground">{formatIQD(rep.sales)}</span>
                    </div>
                    <div className="h-1.5 overflow-hidden rounded-full bg-secondary">
                      <div
                        className="h-full rounded-full bg-gradient-to-l from-primary to-primary/60 transition-all duration-500"
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Recent + Low stock */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Recent invoices */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>أحدث الفواتير</CardTitle>
                <CardDescription>آخر 4 فواتير</CardDescription>
              </div>
              <Link to="/invoices" className="text-xs text-primary hover:underline">
                عرض الكل ←
              </Link>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <table className="data-table">
              <thead>
                <tr>
                  <th>رقم الفاتورة</th>
                  <th>العميل</th>
                  <th>المبلغ</th>
                  <th>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {recentInvoices.map(inv => (
                  <tr key={inv.id}>
                    <td><span className="num-display text-xs text-muted-foreground">{inv.id}</span></td>
                    <td className="font-medium">{inv.customer}</td>
                    <td><span className="num-display">{formatIQD(inv.amount)}</span></td>
                    <td><StatusBadge status={inv.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>

        {/* Low stock */}
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-warning" />
              <CardTitle>مخزون منخفض</CardTitle>
            </div>
            <CardDescription>{lowStockItems.length} مواد تحتاج تجديد</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {lowStockItems.map(item => (
              <div key={item.name} className="flex items-center justify-between rounded-md border border-warning/20 bg-warning/5 px-3 py-2.5">
                <div>
                  <p className="text-sm font-medium">{item.name}</p>
                  <p className="text-xs text-muted-foreground">متبقي {item.remaining} {item.unit}</p>
                </div>
                <Package className="h-4 w-4 text-warning" />
              </div>
            ))}
            <Link to="/inventory?lowStock=true" className="mt-2 block text-center text-xs text-primary hover:underline">
              مشاهدة كل المخزون المنخفض ←
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
