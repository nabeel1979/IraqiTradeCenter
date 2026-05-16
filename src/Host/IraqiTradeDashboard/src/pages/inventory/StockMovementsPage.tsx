import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Construction } from 'lucide-react';

export function StockMovementsPage() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>حركات المخزون</CardTitle>
        <CardDescription>هذه الصفحة قيد البناء في الجزء التالي</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col items-center py-16 text-center">
        <Construction className="mb-3 h-10 w-10 text-primary/60" />
        <p className="text-sm text-muted-foreground">
          سيتم بناء هذه الواجهة الكاملة مع جدول البيانات، الفلاتر، النماذج، والربط بالـ API
        </p>
      </CardContent>
    </Card>
  );
}
