import { Search, Bell, Calendar } from 'lucide-react';
import { useLocation } from 'react-router-dom';

const routeTitles: Record<string, { title: string; description?: string }> = {
  '/': { title: 'لوحة القيادة', description: 'نظرة عامة على أعمال الشركة' },
  '/invoices': { title: 'فواتير المبيعات', description: 'إدارة وعرض جميع الفواتير' },
  '/invoices/new': { title: 'فاتورة مبيعات جديدة', description: 'إنشاء فاتورة جديدة' },
  '/customers': { title: 'العملاء', description: 'إدارة عملاء الشركة' },
  '/sales-reps': { title: 'مندوبو المبيعات', description: 'إدارة المندوبين والعمولات' },
  '/inventory': { title: 'المخزون', description: 'إدارة المواد والكميات' },
  '/inventory/movements': { title: 'حركات المخزون', description: 'سجل دخول وخروج المواد' },
  '/orders': { title: 'الطلبيات الواردة', description: 'طلبيات من تجار المنصة' },
  '/accounting/accounts': { title: 'شجرة الحسابات', description: 'الهيكل المحاسبي الكامل' },
  '/accounting/journal': { title: 'القيود المحاسبية', description: 'القيود اليومية المرحّلة' },
  '/accounting/trial-balance': { title: 'ميزان المراجعة', description: 'الأرصدة المدينة والدائنة' },
  '/settings': { title: 'الإعدادات', description: 'إعدادات النظام والحساب' },
};

export function TopBar() {
  const location = useLocation();
  const meta = routeTitles[location.pathname] ?? { title: 'صفحة' };

  return (
    <header className="sticky top-0 z-30 h-20 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="flex h-full items-center justify-between px-8">
        {/* Title */}
        <div className="flex flex-col gap-0.5">
          <h2 className="font-display text-2xl font-semibold tracking-tight">{meta.title}</h2>
          {meta.description && (
            <p className="text-xs text-muted-foreground">{meta.description}</p>
          )}
        </div>

        {/* Search + Actions */}
        <div className="flex items-center gap-3">
          <div className="relative w-72">
            <Search className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type="search"
              placeholder="بحث سريع..."
              className="w-full rounded-md border border-border bg-secondary/40 py-2 pr-10 pl-3 text-sm placeholder:text-muted-foreground/60 focus:border-primary/50 focus:bg-secondary focus:outline-none focus:ring-2 focus:ring-ring transition-colors"
            />
            <kbd className="absolute left-3 top-1/2 hidden -translate-y-1/2 select-none rounded border border-border/50 bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground sm:inline-block">
              Ctrl K
            </kbd>
          </div>

          <button className="flex h-10 items-center gap-2 rounded-md border border-border bg-secondary/40 px-3 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground">
            <Calendar className="h-4 w-4" />
            <span className="tnum">
              {new Intl.DateTimeFormat('ar-IQ', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date())}
            </span>
          </button>

          <button className="relative flex h-10 w-10 items-center justify-center rounded-md border border-border bg-secondary/40 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground">
            <Bell className="h-4 w-4" />
            <span className="absolute right-2 top-2 h-2 w-2 rounded-full bg-primary ring-2 ring-background" />
          </button>
        </div>
      </div>
    </header>
  );
}
