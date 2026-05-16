import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Construction, Save, X } from 'lucide-react';

export function CreateInvoicePage() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>إنشاء فاتورة مبيعات جديدة</CardTitle>
        <CardDescription>
          هذي الواجهة الأكثر تعقيداً — رح نبنيها بالكامل في الجزء التالي مع:
          اختيار العميل + إضافة مواد + التحقق من المخزون + احتساب الضرائب + معاينة الفاتورة + الإصدار
        </CardDescription>
      </CardHeader>
      <CardContent className="py-16">
        <div className="flex flex-col items-center text-center">
          <Construction className="mb-3 h-10 w-10 text-primary/60" />
          <p className="text-sm text-muted-foreground max-w-md">
            في الجزء التالي رح أبني نموذج كامل: اختيار عميل عبر autocomplete، إضافة بنود متعددة الوحدات،
            احتساب فوري للضريبة والمجموع، تنبيه عند تجاوز الحد الائتماني، ومعاينة قبل الإصدار.
          </p>
          <div className="mt-6 flex gap-2">
            <Button variant="outline"><X className="h-4 w-4" />إلغاء</Button>
            <Button disabled><Save className="h-4 w-4" />حفظ كمسودة</Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
