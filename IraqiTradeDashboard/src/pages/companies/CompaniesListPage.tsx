import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, Building2, CheckCircle2, XCircle, Pencil, ToggleLeft, ToggleRight } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
  DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { EmptyState } from '@/components/shared/EmptyState';
import { LoadingSpinner } from '@/components/shared/LoadingSpinner';
import { PageHeader } from '@/components/shared/PageHeader';
import { companiesApi } from '@/lib/api/companies';
import type { CompanyDto } from '@/types/api';

// ── Validation schema
const companySchema = z.object({
  code: z.string().min(2, 'الرمز مطلوب ولا يقل عن حرفين'),
  nameAr: z.string().min(3, 'الاسم مطلوب ولا يقل عن 3 أحرف'),
  phone: z.string().min(10, 'رقم الهاتف مطلوب'),
  email: z.string().email('البريد الإلكتروني غير صحيح').optional().or(z.literal('')),
  city: z.string().optional(),
  address: z.string().optional(),
  contactPerson: z.string().optional(),
  subscriptionExpiry: z.string().optional(),
  isActive: z.boolean().optional(),
});

type CompanyForm = z.infer<typeof companySchema>;

// ── Helper: format date
function formatDate(iso?: string) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('ar-IQ', {
    year: 'numeric', month: 'short', day: 'numeric',
  });
}

// ── Sub-component: Company Form Dialog
interface CompanyDialogProps {
  open: boolean;
  onClose: () => void;
  editing?: CompanyDto | null;
}

function CompanyDialog({ open, onClose, editing }: CompanyDialogProps) {
  const queryClient = useQueryClient();
  const isEdit = !!editing;

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<CompanyForm>({
    resolver: zodResolver(companySchema),
    defaultValues: editing ? {
      code: editing.code,
      nameAr: editing.nameAr,
      phone: editing.phone,
      email: editing.email ?? '',
      city: editing.city ?? '',
      address: editing.address ?? '',
      contactPerson: editing.contactPerson ?? '',
      subscriptionExpiry: editing.subscriptionExpiry?.slice(0, 10) ?? '',
      isActive: editing.isActive,
    } : { code: '', nameAr: '', phone: '', email: '', city: '', address: '', contactPerson: '', subscriptionExpiry: '', isActive: true },
  });

  const createMutation = useMutation({
    mutationFn: companiesApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['companies'] });
      toast.success('تم إضافة الشركة بنجاح');
      reset();
      onClose();
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: any }) => companiesApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['companies'] });
      toast.success('تم تحديث بيانات الشركة');
      onClose();
    },
  });

  const onSubmit = (values: CompanyForm) => {
    const payload = {
      ...values,
      email: values.email || undefined,
      city: values.city || undefined,
      address: values.address || undefined,
      contactPerson: values.contactPerson || undefined,
      subscriptionExpiry: values.subscriptionExpiry || undefined,
    };

    if (isEdit && editing) {
      updateMutation.mutate({ id: editing.id, data: { ...payload, isActive: editing.isActive } });
    } else {
      createMutation.mutate(payload);
    }
  };

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose(); }}>
      <DialogContent className="max-w-xl" dir="rtl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'تعديل بيانات الشركة' : 'إضافة شركة جديدة'}</DialogTitle>
          <DialogDescription>
            {isEdit ? `تعديل بيانات: ${editing?.nameAr}` : 'أدخل بيانات الشركة الجديدة'}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            {/* Code */}
            <div className="space-y-1.5">
              <Label htmlFor="code">رمز الشركة *</Label>
              <Input id="code" {...register('code')} placeholder="مثال: ITC-001" />
              {errors.code && <p className="text-xs text-destructive">{errors.code.message}</p>}
            </div>

            {/* Name */}
            <div className="space-y-1.5">
              <Label htmlFor="nameAr">اسم الشركة *</Label>
              <Input id="nameAr" {...register('nameAr')} placeholder="الاسم بالعربي" />
              {errors.nameAr && <p className="text-xs text-destructive">{errors.nameAr.message}</p>}
            </div>

            {/* Phone */}
            <div className="space-y-1.5">
              <Label htmlFor="phone">رقم الهاتف *</Label>
              <Input id="phone" {...register('phone')} placeholder="07xxxxxxxxx" dir="ltr" />
              {errors.phone && <p className="text-xs text-destructive">{errors.phone.message}</p>}
            </div>

            {/* Email */}
            <div className="space-y-1.5">
              <Label htmlFor="email">البريد الإلكتروني</Label>
              <Input id="email" {...register('email')} placeholder="info@company.com" dir="ltr" />
              {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
            </div>

            {/* City */}
            <div className="space-y-1.5">
              <Label htmlFor="city">المدينة</Label>
              <Input id="city" {...register('city')} placeholder="بغداد" />
            </div>

            {/* Contact Person */}
            <div className="space-y-1.5">
              <Label htmlFor="contactPerson">المسؤول</Label>
              <Input id="contactPerson" {...register('contactPerson')} placeholder="اسم المسؤول" />
            </div>

            {/* Subscription Expiry */}
            <div className="space-y-1.5">
              <Label htmlFor="subscriptionExpiry">انتهاء الاشتراك</Label>
              <Input id="subscriptionExpiry" type="date" {...register('subscriptionExpiry')} />
            </div>
          </div>

          {/* Address - full width */}
          <div className="space-y-1.5">
            <Label htmlFor="address">العنوان التفصيلي</Label>
            <Input id="address" {...register('address')} placeholder="المنطقة، الشارع، رقم البناية..." />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
              إلغاء
            </Button>
            <Button type="submit" disabled={isPending || isSubmitting}>
              {isPending ? 'جاري الحفظ...' : isEdit ? 'حفظ التعديلات' : 'إضافة الشركة'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ── Main Page
export function CompaniesListPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [activeOnly, setActiveOnly] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCompany, setEditingCompany] = useState<CompanyDto | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['companies', search, activeOnly],
    queryFn: () => companiesApi.list({ search, activeOnly, pageSize: 50 }),
  });

  const toggleMutation = useMutation({
    mutationFn: companiesApi.toggleStatus,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['companies'] });
      toast.success('تم تحديث حالة الشركة');
    },
  });

  const openCreate = () => {
    setEditingCompany(null);
    setDialogOpen(true);
  };

  const openEdit = (company: CompanyDto) => {
    setEditingCompany(company);
    setDialogOpen(true);
  };

  const isExpiringSoon = (expiry?: string) => {
    if (!expiry) return false;
    const days = (new Date(expiry).getTime() - Date.now()) / (1000 * 60 * 60 * 24);
    return days >= 0 && days <= 30;
  };

  const isExpired = (expiry?: string) => {
    if (!expiry) return false;
    return new Date(expiry) < new Date();
  };

  return (
    <div className="space-y-5">
      <PageHeader>
        <h2 className="text-2xl font-bold tracking-tight">الشركات</h2>
        <p className="mt-1 text-sm text-muted-foreground">إدارة الشركات المشتركة في نظام مركز التجارة العراقي</p>
      </PageHeader>

      {/* Filter bar */}
      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <div className="relative flex-1 min-w-[260px]">
            <Search className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="ابحث باسم الشركة، الرمز، أو الهاتف..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pr-10"
            />
          </div>
          <Button
            variant={activeOnly ? 'default' : 'outline'}
            onClick={() => setActiveOnly(!activeOnly)}
          >
            <CheckCircle2 className="h-4 w-4" />
            الشركات النشطة فقط
          </Button>
          <Button onClick={openCreate} className="mr-auto">
            <Plus className="h-4 w-4" />
            شركة جديدة
          </Button>
        </CardContent>
      </Card>

      {/* Results */}
      {isLoading ? (
        <LoadingSpinner text="جاري تحميل الشركات..." />
      ) : error ? (
        <EmptyState
          icon={Building2}
          title="تعذّر تحميل الشركات"
          description="تأكد من اتصال الـ API على المنفذ 6000"
        />
      ) : !data?.items.length ? (
        <EmptyState
          icon={Building2}
          title="لا توجد شركات"
          description={activeOnly ? 'لا توجد شركات نشطة حالياً' : 'ابدأ بإضافة أول شركة في النظام'}
          action={
            <Button onClick={openCreate}>
              <Plus className="h-4 w-4" />
              إضافة شركة
            </Button>
          }
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <table className="data-table">
              <thead>
                <tr>
                  <th className="w-28">الرمز</th>
                  <th>اسم الشركة</th>
                  <th>المسؤول</th>
                  <th>الهاتف</th>
                  <th>المدينة</th>
                  <th>انتهاء الاشتراك</th>
                  <th>الحالة</th>
                  <th className="w-24 text-center">إجراءات</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map(company => {
                  const expired = isExpired(company.subscriptionExpiry);
                  const expiring = isExpiringSoon(company.subscriptionExpiry);

                  return (
                    <tr key={company.id} className="group">
                      <td>
                        <span className="num-display text-xs font-mono text-muted-foreground">
                          {company.code}
                        </span>
                      </td>
                      <td>
                        <div>
                          <p className="font-medium">{company.nameAr}</p>
                          {company.email && (
                            <p className="text-xs text-muted-foreground" dir="ltr">{company.email}</p>
                          )}
                        </div>
                      </td>
                      <td>
                        <span className="text-sm text-muted-foreground">
                          {company.contactPerson ?? '—'}
                        </span>
                      </td>
                      <td>
                        <span className="num-display text-sm" dir="ltr">{company.phone}</span>
                      </td>
                      <td>
                        <span className="text-sm text-muted-foreground">{company.city ?? '—'}</span>
                      </td>
                      <td>
                        {company.subscriptionExpiry ? (
                          <span className={
                            'num-display text-sm ' +
                            (expired ? 'text-destructive font-medium' :
                              expiring ? 'text-warning font-medium' :
                                'text-muted-foreground')
                          }>
                            {formatDate(company.subscriptionExpiry)}
                            {expired && ' (منتهي)'}
                            {expiring && !expired && ' (قريباً)'}
                          </span>
                        ) : (
                          <span className="text-muted-foreground text-sm">—</span>
                        )}
                      </td>
                      <td>
                        {company.isActive ? (
                          <Badge variant="success">نشطة</Badge>
                        ) : (
                          <Badge variant="muted">موقوفة</Badge>
                        )}
                        {expired && company.isActive && (
                          <Badge variant="warning" className="mr-1">منتهية</Badge>
                        )}
                      </td>
                      <td>
                        <div className="flex items-center justify-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => openEdit(company)}
                            className="rounded p-1.5 text-muted-foreground hover:bg-accent/60 hover:text-foreground transition-colors"
                            title="تعديل"
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </button>
                          <button
                            onClick={() => toggleMutation.mutate(company.id)}
                            disabled={toggleMutation.isPending}
                            className="rounded p-1.5 text-muted-foreground hover:bg-accent/60 hover:text-foreground transition-colors"
                            title={company.isActive ? 'إيقاف' : 'تفعيل'}
                          >
                            {company.isActive
                              ? <ToggleRight className="h-3.5 w-3.5 text-success" />
                              : <ToggleLeft className="h-3.5 w-3.5" />
                            }
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </CardContent>

          {/* Footer */}
          <div className="flex items-center justify-between border-t border-border/40 px-6 py-3 text-xs text-muted-foreground">
            <span className="flex items-center gap-3">
              <span className="flex items-center gap-1">
                <CheckCircle2 className="h-3 w-3 text-success" />
                نشطة: {data.items.filter(c => c.isActive).length}
              </span>
              <span className="flex items-center gap-1">
                <XCircle className="h-3 w-3 text-muted-foreground" />
                موقوفة: {data.items.filter(c => !c.isActive).length}
              </span>
            </span>
            <span>عرض {data.items.length} من {data.totalCount} شركة</span>
          </div>
        </Card>
      )}

      {/* Dialog */}
      <CompanyDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        editing={editingCompany}
      />
    </div>
  );
}
