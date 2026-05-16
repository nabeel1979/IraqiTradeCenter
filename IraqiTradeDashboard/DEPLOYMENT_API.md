# 🔗 دليل ربط الـ Dashboard بالـ API

## 🎯 اسم الـ API

النظام يستخدم **`api_iraqitradecenter_company`** كاسم موحّد للـ Companies API.

---

## 🌐 خريطة البيئات الثلاث

| البيئة | الاستخدام | الـ URL |
|--------|----------|---------|
| **Development** | على لابتوبك | `http://localhost:6000` |
| **Staging** | للاختبار قبل الإنتاج | `https://staging-api-company.iraqitradecenter.com` |
| **Production** | للعملاء الفعليين | `https://api-company.iraqitradecenter.com` |

---

## ⚙️ ملفات الإعدادات

كل بيئة لها ملف `.env` خاص فيها:

```
.env.development     ← التطوير المحلي
.env.staging         ← السيرفر الاختباري
.env.production      ← السيرفر الإنتاج
.env.example         ← قالب مرجعي
```

### مثال على `.env.production`:
```bash
VITE_API_URL=https://api-company.iraqitradecenter.com
VITE_APP_NAME=مركز التجارة العراقي
VITE_APP_ENV=production
VITE_ENABLE_DEVTOOLS=false
```

---

## 🚀 أوامر التشغيل والبناء

```bash
# تطوير محلي (يستخدم .env.development)
npm run dev

# بناء للإنتاج (يستخدم .env.production)
npm run build

# بناء للاختبار/Staging (يستخدم .env.staging)
npm run build:staging
```

---

## 📋 خطة الـ Deployment

### الخطوة 1: على السيرفر - إعداد IIS Application

```powershell
# في PowerShell كـ Administrator
Import-Module WebAdministration

# إنشاء Application Pool
New-WebAppPool -Name "api_iraqitradecenter_company"
Set-ItemProperty "IIS:\AppPools\api_iraqitradecenter_company" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\api_iraqitradecenter_company" -Name startMode -Value "AlwaysRunning"

# إنشاء Site
$path = "C:\inetpub\api_iraqitradecenter_company"
New-Item -ItemType Directory -Path $path -Force

New-Website -Name "api_iraqitradecenter_company" `
    -PhysicalPath $path `
    -ApplicationPool "api_iraqitradecenter_company" `
    -Port 6000

# Application Pool للـ Dashboard أيضاً
New-WebAppPool -Name "dashboard_iraqitradecenter_company"
Set-ItemProperty "IIS:\AppPools\dashboard_iraqitradecenter_company" -Name managedRuntimeVersion -Value ""

$dashboardPath = "C:\inetpub\dashboard_iraqitradecenter_company"
New-Item -ItemType Directory -Path $dashboardPath -Force

New-Website -Name "dashboard_iraqitradecenter_company" `
    -PhysicalPath $dashboardPath `
    -ApplicationPool "dashboard_iraqitradecenter_company" `
    -Port 3000
```

### الخطوة 2: إعداد الـ Subdomains في DNS

في لوحة تحكم الدومين (Namecheap/Cloudflare/إلخ):

```
Type    Name                         Value              TTL
─────────────────────────────────────────────────────────────
A       api-company                  65.20.159.30       3600
A       dashboard                    65.20.159.30       3600
A       www                          65.20.159.30       3600
A       @                            65.20.159.30       3600
```

### الخطوة 3: إعداد HTTPS bindings في IIS

```powershell
# بعد الحصول على شهادة SSL (Let's Encrypt مثلاً)
New-WebBinding -Name "api_iraqitradecenter_company" `
    -Protocol "https" `
    -Port 443 `
    -HostHeader "api-company.iraqitradecenter.com" `
    -SslFlags 1

New-WebBinding -Name "dashboard_iraqitradecenter_company" `
    -Protocol "https" `
    -Port 443 `
    -HostHeader "dashboard.iraqitradecenter.com" `
    -SslFlags 1
```

### الخطوة 4: بناء ونشر الـ Dashboard

```bash
# على جهازك:
cd IraqiTradeDashboard
npm install
npm run build

# انسخ مجلد dist/ إلى السيرفر
# المسار على السيرفر: C:\inetpub\dashboard_iraqitradecenter_company\
```

أو من PowerShell على جهازك:
```powershell
# نسخ عبر SCP/RDP/Network share
Copy-Item -Path .\dist\* -Destination "\\65.20.159.30\C$\inetpub\dashboard_iraqitradecenter_company\" -Recurse -Force
```

### الخطوة 5: إضافة web.config للـ React Router

أنشئ ملف `web.config` داخل `C:\inetpub\dashboard_iraqitradecenter_company\`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- React Router - كل الـ routes تذهب إلى index.html -->
        <rule name="React Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
            <add input="{REQUEST_URI}" pattern="^/api" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
    
    <!-- Cache headers للأداء -->
    <staticContent>
      <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="365.00:00:00" />
      <remove fileExtension=".html" />
      <mimeMap fileExtension=".html" mimeType="text/html" />
    </staticContent>
    
    <!-- Headers أمان -->
    <httpProtocol>
      <customHeaders>
        <add name="X-Frame-Options" value="SAMEORIGIN" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
        <add name="X-XSS-Protection" value="1; mode=block" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
```

---

## 🛡️ تفعيل CORS في الـ API

في الـ `Companies API` (Program.cs)، تأكد من السماح للـ Dashboard:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",                          // dev
                "https://staging-dashboard.iraqitradecenter.com", // staging
                "https://dashboard.iraqitradecenter.com"          // prod
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// قبل MapControllers
app.UseCors("AllowDashboard");
```

---

## 🧪 اختبار الاتصال

### من الـ Dashboard للـ API:

```bash
# في وضع التطوير
curl http://localhost:6000/swagger
# يجب أن يفتح Swagger UI

# في الإنتاج
curl https://api-company.iraqitradecenter.com/swagger
```

### من المتصفح:

افتح Developer Tools → Network → سجل دخول → يجب أن تشوف:
```
POST https://api-company.iraqitradecenter.com/api/auth/login
Status: 200 OK
Response: { success: true, data: { token: "..." } }
```

---

## 🔄 خلاصة Workflow الكامل

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│   User → dashboard.iraqitradecenter.com                │
│              │                                          │
│              ▼                                          │
│       [React Dashboard]                                 │
│              │                                          │
│              │ HTTPS                                    │
│              ▼                                          │
│   api-company.iraqitradecenter.com                     │
│              │                                          │
│              ▼                                          │
│   [api_iraqitradecenter_company]                       │
│   (IIS Application Pool)                                │
│              │                                          │
│              ▼                                          │
│   [SQL Server - IraqiTradeCenter_Company_001]          │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## ✅ Checklist Deployment

### السيرفر:
- [ ] Application Pool `api_iraqitradecenter_company` منشأ
- [ ] Site للـ API على port 6000
- [ ] Application Pool `dashboard_iraqitradecenter_company` منشأ
- [ ] Site للـ Dashboard على port 3000 (أو 80/443)
- [ ] الـ APIs منشورة في `C:\inetpub\api_iraqitradecenter_company\`
- [ ] الـ Dashboard build منسوخ إلى `C:\inetpub\dashboard_iraqitradecenter_company\`
- [ ] ملف `web.config` للـ React Router موجود

### DNS:
- [ ] `api-company.iraqitradecenter.com` → السيرفر IP
- [ ] `dashboard.iraqitradecenter.com` → السيرفر IP
- [ ] `www.iraqitradecenter.com` → السيرفر IP

### SSL:
- [ ] شهادة SSL للـ API
- [ ] شهادة SSL للـ Dashboard
- [ ] HTTPS bindings في IIS

### الأمان:
- [ ] CORS مضبوط في الـ API
- [ ] JWT Key مشترك بين Parent و Company
- [ ] Firewall يسمح فقط بـ 80/443 من الإنترنت
- [ ] SQL Server خلف Firewall (localhost فقط)
