# 🎨 لوحة الشركة الجملة - مركز التجارة العراقي

داشبورد احترافي لإدارة الشركات الجملة في منصة مركز التجارة العراقي.  
مبني بـ **React 18 + TypeScript + Vite + Tailwind CSS + shadcn/ui**.

## ✨ الميزات

- 🌙 **Dark Mode الافتراضي** بطابع ميسوبوتامي راقي (لون ذهبي عتيق)
- 🌐 **عربي-أولاً (RTL)** مع خط IBM Plex Sans Arabic احترافي
- 📱 **متجاوب** على كل الأجهزة
- 🔐 **JWT Authentication** متكامل مع الـ Backend
- ⚡ **TanStack Query** للـ data fetching الذكي
- 🎯 **TypeScript** بالكامل، Types مطابقة للـ DTOs

## 🚀 التشغيل السريع

### المتطلبات
- Node.js 18+ ([تحميل](https://nodejs.org/))
- npm أو yarn أو pnpm

### الخطوات

```bash
# 1) نصب الحزم
npm install

# 2) شغّل في وضع التطوير
npm run dev

# 3) افتح المتصفح على
# http://localhost:3000
```

### اتصال الـ Backend

افتراضياً، الـ Vite proxy يحوّل طلبات `/api/*` إلى `http://localhost:6000` (الـ Companies API).

لو الـ Backend على عنوان مختلف، عدّل `vite.config.ts`:

```typescript
server: {
  proxy: {
    '/api': {
      target: 'http://YOUR_API_URL:6000',  // ← غيّر هنا
      changeOrigin: true,
    },
  },
},
```

## 📁 هيكل المشروع

```
src/
├── components/
│   ├── ui/              # shadcn/ui components
│   ├── layout/          # Sidebar, TopBar, Layout
│   └── shared/          # StatCard, EmptyState, ...
├── pages/
│   ├── auth/            # Login
│   ├── dashboard/       # الـ KPIs والرسومات
│   ├── invoices/        # الفواتير
│   ├── inventory/       # المخزون
│   ├── customers/       # العملاء
│   ├── sales-reps/      # المندوبون
│   ├── orders/          # الطلبيات
│   └── accounting/      # المحاسبة
├── lib/
│   ├── api/             # API clients (axios)
│   ├── auth/            # Zustand store + Guard
│   └── utils.ts         # formatIQD, formatDate, cn
├── types/
│   └── api.ts           # TypeScript types من الـ DTOs
└── globals.css          # نظام التصميم
```

## 🎨 نظام التصميم

### الألوان
- **Background**: `#0F0F11` - أسود دافئ
- **Primary**: `#D4A876` - ذهب عتيق
- **Card**: `#16161A` - رمادي داكن دافئ
- **Border**: `#26262C` - حدود خفيفة

### الخطوط
- **Display** (العناوين): Cormorant Garamond
- **Body**: IBM Plex Sans Arabic
- **Numbers**: IBM Plex Mono (tabular-nums)

## 🔌 الصفحات المبنية حالياً

| الصفحة | الحالة |
|---------|--------|
| 🔐 تسجيل الدخول | ✅ كامل + متصل بـ API |
| 📊 لوحة القيادة | ✅ كامل + رسوم بيانية |
| 📦 قائمة المواد | ✅ كامل + متصل بـ API |
| 🧾 قائمة الفواتير | ✅ هيكل جاهز |
| ➕ إنشاء فاتورة | ⏳ في الجزء التالي |
| 👥 العملاء | ⏳ في الجزء التالي |
| 🧑‍💼 المندوبون | ⏳ في الجزء التالي |
| 📋 الطلبيات | ⏳ في الجزء التالي |
| 💰 المحاسبة (3 صفحات) | ⏳ في الجزء التالي |

## 📦 بناء للإنتاج

```bash
npm run build
```

المخرجات في مجلد `dist/`. ارفعهم لأي static host (Nginx, Vercel, Netlify, IIS).

### نشر على IIS
1. ابني المشروع: `npm run build`
2. انسخ محتوى `dist/` إلى مجلد IIS Site
3. أضف ملف `web.config` للتعامل مع React Router:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="React Router">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

## 🔗 API Endpoints المستخدمة

كل الـ endpoints تطلب JWT في `Authorization: Bearer <token>`:

- `POST /api/auth/login` → تسجيل دخول
- `GET /api/items` → قائمة المواد
- `POST /api/items` → إضافة مادة
- `POST /api/salesinvoices` → إنشاء فاتورة
- `POST /api/salesinvoices/{id}/payments` → تسجيل دفعة
- `GET /api/accounts/tree` → شجرة الحسابات
- `GET /api/accounts/trial-balance` → ميزان المراجعة
- `GET /api/incomingorders/pending` → الطلبيات المعلقة

## 🛠️ Stack الكامل

| المكتبة | الاستخدام |
|---------|--------|
| **React 18** | UI framework |
| **TypeScript** | Type safety |
| **Vite** | Build tool |
| **Tailwind CSS** | Styling |
| **shadcn/ui** | Component primitives |
| **TanStack Query** | Server state |
| **Zustand** | Client state (auth) |
| **React Router** | Routing |
| **React Hook Form + Zod** | النماذج والتحقق |
| **Recharts** | الرسوم البيانية |
| **Axios** | HTTP client |
| **Sonner** | Notifications |
| **Lucide React** | Icons |

## 🎯 الخطوات التالية

الـ Foundation كامل. الباقي:
1. صفحة إنشاء فاتورة (الأهم - معقدة)
2. قائمة العملاء + كشف الحساب
3. شجرة الحسابات التفاعلية
4. صفحة المندوبين مع احتساب العمولات
5. صفحة الطلبيات الواردة مع التأكيد
6. ميزان المراجعة بشكل تفاعلي
