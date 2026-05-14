# مركز التجارة العراقي - الشركات API

نظام إدارة الشركات في منصة "مركز التجارة العراقي" — مبني بمعمارية **Modular Monolith** على **ASP.NET 8** + **EF Core 8**.

## 🏛️ المعمارية

كل شركة في المنصة عندها قاعدة بيانات منفصلة + نسخة من هذا الـ API. النظام مقسم إلى **3 مودولز معزولة** داخل solution واحد:

```
┌─────────────────────────────────────────────────────────────┐
│           IraqiTradeCenterCompany.API (Host)               │
│              يجمع المودولز الثلاث في deployment واحد         │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   ┌─────────┐          ┌──────────┐         ┌─────────┐
   │ المحاسبة  │          │ المستودعات │         │  المتجر  │
   │ acc.*   │          │  inv.*   │         │ store.* │
   └─────────┘          └──────────┘         └─────────┘
        ▲                     ▲                     │
        │                     │                     │
        └─────IAccountingService              IInventoryService
                                                    │
                                       ┌────────────┴──────────┐
                                       │  Store يستدعي عبر      │
                                       │  Contracts فقط         │
                                       └─────────────────────────┘
```

### المبادئ الأساسية

1. **عزل صارم**: Store لا يعرف Accounting.Domain أو Inventory.Domain — يستخدم Contracts فقط (واجهات + DTOs).
2. **Schemas منفصلة** بنفس قاعدة البيانات الفيزيائية:
   - `acc.*` — جداول المحاسبة
   - `inv.*` — جداول المستودعات
   - `store.*` — جداول المتجر
3. **3 DbContexts منفصلة** + **3 جداول Migrations منفصلة** (`__EFMigrations_Accounting/Inventory/Store`).
4. **Transactions عبر المودولز** عبر `TransactionScope` — كلهم على نفس الـ connection فيدخلون بنفس الـ DB transaction.

## 📁 هيكل المشاريع (11 مشروع)

```
src/
├── Shared/
│   └── IraqiTradeCenterCompany.SharedKernel/
│       ├── Common/BaseEntity, BaseEntityGuid
│       ├── Exceptions/DomainException, NotFoundException
│       ├── Models/Result<T>, PagedResult<T>
│       ├── Behaviors/ValidationBehavior, LoggingBehavior
│       └── Interfaces/ICurrentUserService, IDateTimeService
│
├── Modules/
│   ├── Accounting/                          [المحاسبة]
│   │   ├── .Domain         (Account, JournalEntry, FiscalYear, AccountingPeriod)
│   │   ├── .Application    (Contracts/IAccountingService - PUBLIC) + Features
│   │   └── .Infrastructure (AccountingDbContext + Services + Seed)
│   │
│   ├── Inventory/                           [المستودعات]
│   │   ├── .Domain         (Item, StockMovement, Warehouse, UnitOfMeasure)
│   │   ├── .Application    (Contracts/IInventoryService - PUBLIC) + Features
│   │   └── .Infrastructure (InventoryDbContext + Services + Seed)
│   │
│   └── Store/                               [المتجر]
│       ├── .Domain         (Customer, SalesRep, SalesInvoice, IncomingOrder)
│       ├── .Application    + يستخدم IAccountingService + IInventoryService
│       └── .Infrastructure (StoreDbContext)
│
└── Host/
    └── IraqiTradeCenterCompany.API
```

## 🔗 التواصل بين المودولز

### Accounting ← Store ، Inventory

```csharp
public interface IAccountingService
{
    Task<int> CreateAutomaticJournalEntryAsync(CreateAutomaticEntryRequest request, CancellationToken ct);
    Task<int> GetAccountIdByCodeAsync(string code, CancellationToken ct);
    Task EnsurePeriodOpenAsync(DateTime date, CancellationToken ct);
}
```

البيانات المرسلة DTOs فقط — `AccountCode` (مثل `1.2.01`) وليس `AccountId`. هكذا Store لا يعرف شيء عن الـ entities الداخلية للمحاسبة.

### Inventory ← Store

```csharp
public interface IInventoryService
{
    Task<bool> CheckStockAvailabilityAsync(int itemId, int unitId, decimal quantity, CancellationToken ct);
    Task<int> RecordSalesOutAsync(StockOutRequest request, CancellationToken ct);
    Task<int> RecordSalesReturnAsync(StockReturnRequest request, CancellationToken ct);
    Task<ItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct);
    Task<int?> GetDefaultWarehouseIdAsync(CancellationToken ct);
}
```

## 💎 مثال على Cross-Module: إنشاء فاتورة مبيعات

`CreateSalesInvoiceHandler` (في Store) ينفذ كل ذلك في **TransactionScope واحد**:

```
1) تحقق من العميل (Store DB)
2) EnsurePeriodOpenAsync — فترة محاسبية مفتوحة (Accounting)
3) GetItemSnapshotAsync لكل مادة (Inventory)
4) CheckStockAvailabilityAsync (Inventory)
5) بناء الفاتورة + Issue (Store)
6) فحص CreditLimit (Store)
7) RecordSalesOutAsync لكل سطر (Inventory)
8) AdjustBalance للعميل (Store)
9) CreateAutomaticJournalEntryAsync (Accounting):
     مدين  ذمم العملاء (1.2.01)    = إجمالي الفاتورة
     دائن  إيرادات المبيعات (4.1.01) = صافي بعد الخصم
     دائن  ضريبة مستحقة (2.1.02)   = الضريبة
10) LinkJournalEntry — ربط الفاتورة بالقيد (Store)
11) إذا من طلبية، Confirm الطلبية (Store)
```

## 📊 شجرة الحسابات العراقية (تُزرع تلقائياً)

| الكود   | الحساب                       | النوع     | الطبيعة |
|--------|------------------------------|-----------|---------|
| 1.1.01 | الصندوق                       | أصول     | مدين    |
| 1.1.02 | البنك                         | أصول     | مدين    |
| 1.2.01 | ذمم العملاء                   | أصول     | مدين    |
| 1.3.01 | مخزون البضاعة                  | أصول     | مدين    |
| 2.1.01 | ذمم الموردين                  | خصوم     | دائن    |
| 2.1.02 | ضريبة مستحقة الدفع              | خصوم     | دائن    |
| 2.1.03 | عمولات مستحقة الدفع             | خصوم     | دائن    |
| 2.1.04 | رواتب مستحقة الدفع              | خصوم     | دائن    |
| 3.1.01 | رأس المال                     | حقوق ملكية | دائن   |
| 4.1.01 | إيرادات المبيعات                | إيرادات   | دائن    |
| 4.1.02 | خصومات ممنوحة                  | إيرادات   | مدين    |
| 4.1.03 | مرتجع المبيعات                 | إيرادات   | مدين    |
| 5.1.01 | تكلفة البضاعة المباعة            | مصروفات   | مدين    |
| 5.2.01 | مصروف عمولات المندوبين           | مصروفات   | مدين    |
| 5.2.02 | نقل وشحن                      | مصروفات   | مدين    |
| 5.3.01 | الرواتب                       | مصروفات   | مدين    |
| 5.3.02 | الإيجار                        | مصروفات   | مدين    |
| 5.4.01 | كهرباء وماء                    | مصروفات   | مدين    |
| 5.4.02 | اتصالات وإنترنت                 | مصروفات   | مدين    |

## ⚙️ الميزات

### المحاسبة
- ✅ القيد المزدوج Double-Entry (التحقق من توازن المدين = الدائن قبل الترحيل)
- ✅ سنوات مالية + فترات شهرية (12 فترة لكل سنة)
- ✅ منع القيود في فترة مغلقة (`ClosedPeriodException`)
- ✅ القيود التلقائية المولّدة من Store و Inventory
- ✅ ميزان المراجعة Trial Balance
- ✅ شجرة الحسابات الهرمية (3 مستويات)
- ✅ عكس القيود (Reversal)

### المستودعات
- ✅ **ثلاث وحدات قياس لكل مادة** (أساس، متوسط، كبير) مع معاملات تحويل
- ✅ المخزون دائماً بوحدة الأساس داخلياً (`StockBaseQuantity`)
- ✅ سعر مستقل لكل وحدة
- ✅ تتبع كامل لحركات المخزون مع QtyBefore/QtyAfter
- ✅ منع البيع عند نفاد المخزون (`InsufficientStockException`)
- ✅ مستوى مخزون أدنى مع تنبيه (`IsLowStock`)
- ✅ Optimistic Concurrency على Item (RowVersion)

### المتجر
- ✅ ربط مع المنصة الأم (PlatformUserId, PlatformTraderId, PlatformOrderId)
- ✅ حالات الفاتورة State Machine (Draft → Issued → Paid/PartiallyPaid/Cancelled)
- ✅ نوعين من عمولات المندوبين: ثابتة Fixed + متدرجة Tiered
- ✅ كشف حساب عميل مع رصيد متراكم
- ✅ حد ائتماني للعميل + فحص قبل الفاتورة
- ✅ تأكيد طلبيات قادمة من المنصة الأم → فاتورة + قيد + خصم مخزون

## 🛠️ التشغيل

### المتطلبات
- .NET 8 SDK
- SQL Server (Express أو أعلى)
- نسخة من Parent Backend (لـ JWT المشترك)

### الخطوات

```bash
cd src/Host/IraqiTradeCenterCompany.API
dotnet restore
dotnet build
dotnet run
```

عند أول تشغيل سيتم:
1. إنشاء قاعدة البيانات `IraqiTradeCenter_Company_001`
2. إنشاء الـ schemas الثلاثة (`acc`, `inv`, `store`)
3. زرع شجرة الحسابات + السنة المالية + الفترات
4. زرع وحدات القياس + المخزن الافتراضي

افتح: **https://localhost:6001/swagger**

### إنشاء Migrations (اختياري)

كل مودول له migrations منفصلة:

```bash
# المحاسبة
dotnet ef migrations add Initial \
  --project src/Modules/Accounting/IraqiTradeCenterCompany.Modules.Accounting.Infrastructure \
  --startup-project src/Host/IraqiTradeCenterCompany.API \
  --context AccountingDbContext

# المستودعات
dotnet ef migrations add Initial \
  --project src/Modules/Inventory/IraqiTradeCenterCompany.Modules.Inventory.Infrastructure \
  --startup-project src/Host/IraqiTradeCenterCompany.API \
  --context InventoryDbContext

# المتجر
dotnet ef migrations add Initial \
  --project src/Modules/Store/IraqiTradeCenterCompany.Modules.Store.Infrastructure \
  --startup-project src/Host/IraqiTradeCenterCompany.API \
  --context StoreDbContext
```

## 🇮🇶 قواعد عراقية مدمجة

- العملة: **IQD** بصيغة `decimal(18,3)` (الدينار يقبل ثلاث منازل عشرية)
- الهاتف: regex `^07[0-9]{9}$`
- التوقيت: Baghdad (`Arabic Standard Time`)
- كل الرسائل والـ Domain Exceptions بالعربي
- التقريب: 3 منازل عشرية في كل العمليات

## 📡 الـ Endpoints

| Endpoint | الوصف |
|---------|------|
| `GET    /api/accounts/tree` | شجرة الحسابات |
| `POST   /api/accounts/journal-entries` | قيد يدوي |
| `GET    /api/accounts/trial-balance?from=&to=` | ميزان المراجعة |
| `POST   /api/items` | إضافة مادة |
| `GET    /api/items` | قائمة المواد |
| `POST   /api/items/stock-movements` | حركة مخزون |
| `POST   /api/salesreps` | إضافة مندوب |
| `POST   /api/salesreps/{id}/calculate-commission` | احتساب عمولة |
| `GET    /api/salesreps/{id}/performance` | أداء المندوب |
| `POST   /api/salesinvoices` | إنشاء فاتورة مبيعات |
| `POST   /api/salesinvoices/{id}/payments` | تسجيل دفعة |
| `GET    /api/customers/{id}/statement` | كشف حساب العميل |
| `GET    /api/incomingorders/pending` | الطلبيات المعلقة |
| `POST   /api/incomingorders/{id}/confirm` | تأكيد طلبية → فاتورة |

## 🔮 المستقبل

- 🔜 Frontend Dashboard للشركات (React + Tailwind)
- 🔜 Mobile App للتجار (React Native)
- 🔜 خدمة المزامنة بين Parent DB و Company DBs
- 🔜 تكامل بوابات الدفع العراقية (ZainCash, AsiaHawala, FastPay)
- 🔜 SignalR للإشعارات الفورية
- 🔜 قائمة الدخل + الميزانية العمومية (تقارير ختامية)
