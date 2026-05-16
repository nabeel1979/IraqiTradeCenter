import { NavLink, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Receipt, Package, Users, UserCog, Inbox,
  BookOpen, Settings, LogOut, Sparkles, TrendingUp
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/lib/auth/auth-store';
import { Separator } from '@/components/ui/separator';

interface NavItem {
  to: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
}

interface NavGroup {
  title: string;
  items: NavItem[];
}

const navigation: NavGroup[] = [
  {
    title: 'الرئيسية',
    items: [{ to: '/', label: 'لوحة القيادة', icon: LayoutDashboard }],
  },
  {
    title: 'المبيعات',
    items: [
      { to: '/invoices', label: 'الفواتير', icon: Receipt },
      { to: '/invoices/new', label: 'فاتورة جديدة', icon: Sparkles },
      { to: '/orders', label: 'الطلبيات الواردة', icon: Inbox },
      { to: '/customers', label: 'العملاء', icon: Users },
      { to: '/sales-reps', label: 'المندوبون', icon: UserCog },
    ],
  },
  {
    title: 'المخزون',
    items: [
      { to: '/inventory', label: 'المواد', icon: Package },
      { to: '/inventory/movements', label: 'حركات المخزون', icon: TrendingUp },
    ],
  },
  {
    title: 'المحاسبة',
    items: [
      { to: '/accounting/accounts', label: 'شجرة الحسابات', icon: BookOpen },
      { to: '/accounting/journal', label: 'القيود', icon: BookOpen },
      { to: '/accounting/trial-balance', label: 'ميزان المراجعة', icon: BookOpen },
    ],
  },
];

export function Sidebar() {
  const location = useLocation();
  const user = useAuthStore(s => s.user);
  const logout = useAuthStore(s => s.logout);

  return (
    <aside className="fixed inset-y-0 right-0 z-40 flex w-72 flex-col border-l border-border/60 bg-card/30 backdrop-blur-xl">
      {/* Brand */}
      <div className="relative flex h-20 items-center gap-3 border-b border-border/60 px-6">
        <div className="relative">
          <div className="absolute inset-0 rounded-full bg-primary/30 blur-md" />
          <div className="relative flex h-11 w-11 items-center justify-center rounded-full bg-gradient-to-br from-primary to-primary/60 shadow-lg">
            <svg viewBox="0 0 24 24" className="h-5 w-5 text-primary-foreground" fill="currentColor">
              <path d="M12 2l2.4 7.2H22l-6.2 4.4L18.4 22 12 17.4 5.6 22l2.6-8.4L2 9.2h7.6z" />
            </svg>
          </div>
        </div>
        <div>
          <h1 className="font-display text-lg font-semibold leading-none tracking-tight">
            مركز التجارة
          </h1>
          <p className="mt-1 text-[11px] uppercase tracking-[0.18em] text-primary/70">
            Iraqi Trade Center
          </p>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto px-4 py-6">
        {navigation.map((group, gi) => (
          <div key={group.title} className={cn(gi > 0 && 'mt-7')}>
            <h3 className="px-3 pb-2 text-[10px] uppercase tracking-[0.18em] text-muted-foreground/70">
              {group.title}
            </h3>
            <div className="space-y-0.5">
              {group.items.map(item => {
                const isActive = location.pathname === item.to ||
                  (item.to !== '/' && location.pathname.startsWith(item.to));
                return (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className={cn(
                      'group relative flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium transition-all',
                      isActive
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-accent/40 hover:text-foreground'
                    )}
                  >
                    {isActive && (
                      <span className="absolute right-0 top-1/2 h-5 w-[2px] -translate-y-1/2 rounded-full bg-primary" />
                    )}
                    <item.icon className={cn(
                      'h-[18px] w-[18px] transition-colors',
                      isActive ? 'text-primary' : 'text-muted-foreground/80 group-hover:text-foreground/90'
                    )} />
                    <span>{item.label}</span>
                    {item.badge && (
                      <span className="mr-auto rounded-full bg-primary/20 px-2 py-0.5 text-[10px] font-medium text-primary">
                        {item.badge}
                      </span>
                    )}
                  </NavLink>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* User card */}
      <div className="p-4">
        <Separator className="mb-4" />
        <div className="flex items-center gap-3 rounded-lg p-2">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-primary/30 to-primary/10 text-sm font-medium text-primary ring-1 ring-primary/20">
            {user?.fullName?.[0] ?? 'م'}
          </div>
          <div className="flex-1 overflow-hidden">
            <p className="truncate text-sm font-medium">{user?.fullName ?? 'المستخدم'}</p>
            <p className="truncate text-xs text-muted-foreground" dir="ltr">{user?.phone ?? '—'}</p>
          </div>
          <button
            onClick={logout}
            className="rounded-md p-2 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
            title="تسجيل خروج"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
        <NavLink
          to="/settings"
          className="mt-2 flex items-center gap-2 rounded-md px-2 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-accent/40 hover:text-foreground"
        >
          <Settings className="h-3.5 w-3.5" />
          الإعدادات
        </NavLink>
      </div>
    </aside>
  );
}
