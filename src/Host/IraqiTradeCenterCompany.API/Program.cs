using System.Text;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Extensions;
using IraqiTradeCenterCompany.API.Licensing;
using IraqiTradeCenterCompany.API.Middlewares;
using IraqiTradeCenterCompany.API.Trash;
using IraqiTradeCenterCompany.Modules.Accounting.Application;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Seed;
using IraqiTradeCenterCompany.Modules.Inventory.Application;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Seed;
using IraqiTradeCenterCompany.Modules.Store.Application;
using IraqiTradeCenterCompany.Modules.Store.Infrastructure;
using IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Behaviors;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
// إعدادات محلية/سيرفر (مفاتيح وسلاسل اتصال) — الملف مُستثنى من Git
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
// متغيرات البيئة بعد الملف المحلي حتى تتجاوز سلسلة الاتصال عند التشغيل على أجهزة مختلفة
builder.Configuration.AddEnvironmentVariables();

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ============ تسجيل المودولز ============
// 1) Application Layer لكل مودول
builder.Services.AddAccountingApplication();
builder.Services.AddInventoryApplication();
builder.Services.AddStoreApplication();

// 2) Infrastructure Layer لكل مودول (DbContexts + Services)
builder.Services.AddAccountingInfrastructure(builder.Configuration);
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddStoreInfrastructure(builder.Configuration);

// Auth DbContext — نفس قاعدة البيانات مع schema مستقل
builder.Services.AddDbContext<AuthDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Permissions service + كاش في الذاكرة
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IVoucherTypePermissionsSync, VoucherTypePermissionsSync>();
builder.Services.AddUnifiedTrash();
builder.Services.AddSystemLicensing(builder.Configuration);

// 3) MediatR Behaviors المشتركة
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// 4) خدمات مشتركة
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
// ‎سجل المراقبة (Audit) — يكتب لـ auth.AuditLogs في كل عملية حسّاسة.
builder.Services.AddScoped<IraqiTradeCenterCompany.SharedKernel.Interfaces.IAuditLogger,
    IraqiTradeCenterCompany.API.Auth.Auditing.AuditLogger>();

// ‎خدمة الإشعارات الداخلية — تُرسل إشعارات للمستخدمين عند أحداث المناقلات.
builder.Services.AddScoped<IraqiTradeCenterCompany.SharedKernel.Interfaces.INotificationService,
    IraqiTradeCenterCompany.API.Auth.Notifications.NotificationService>();

// ‎أرشيف المرفقات (Attachments):
//   • الإعدادات (المسار المحلي / مفاتيح R2 / الحد الأقصى للحجم) تُخزَّن في
//     جدول auth.AttachmentStorageSettings وتُعدَّل من واجهة الإعدادات.
//   • appsettings.json يُستعمل فقط كقيم افتراضية (Boot-strap قبل أول حفظ).
//   • Registry يقدّم التطبيق المناسب بحسب اسم المزوّد المحفوظ على كل مرفق.
builder.Services.Configure<IraqiTradeCenterCompany.API.Attachments.AttachmentStorageOptions>(
    builder.Configuration.GetSection(IraqiTradeCenterCompany.API.Attachments.AttachmentStorageOptions.SectionName));
builder.Services.AddScoped<IraqiTradeCenterCompany.API.Settings.IAttachmentSettingsService,
    IraqiTradeCenterCompany.API.Settings.AttachmentSettingsService>();
builder.Services.AddScoped<IraqiTradeCenterCompany.API.Attachments.LocalDiskAttachmentStorage>();
builder.Services.AddScoped<IraqiTradeCenterCompany.API.Attachments.R2AttachmentStorage>();
builder.Services.AddScoped<IraqiTradeCenterCompany.API.Attachments.IAttachmentStorageRegistry,
    IraqiTradeCenterCompany.API.Attachments.AttachmentStorageRegistry>();

// 5) Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6) JWT - مفتاح موحد مع Parent للـ SSO
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.SaveToken = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer, ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// 7) CORS — في Development يسمح لكل الأصول، في Production يقرأ القائمة من الإعدادات
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    if (allowedOrigins is { Length: > 0 })
    {
        opt.AddPolicy("AllowFrontends", p =>
            p.WithOrigins(allowedOrigins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials());
    }
});

// 8) Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "مركز التجارة العراقي - الشركات API",
        Version = "v1",
        Description = "API لإدارة الشركات: محاسبة، مستودعات، متجر"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer Token",
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ============ Migrations + Seed ============
try
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var accountingDb = sp.GetRequiredService<AccountingDbContext>();
    var inventoryDb  = sp.GetRequiredService<InventoryDbContext>();
    var storeDb      = sp.GetRequiredService<StoreDbContext>();

    var authDb = sp.GetRequiredService<AuthDbContext>();

    await authDb.Database.MigrateAsync();
    await accountingDb.Database.MigrateAsync();
    await inventoryDb.Database.MigrateAsync();
    await storeDb.Database.MigrateAsync();

    // 1) Seed المحاسبي أولاً ليكون لدينا voucher types (إن وُجدت) قبل مزامنة الصلاحيات
    await ChartOfAccountsSeeder.SeedAsync(accountingDb);
    await FiscalYearSeeder.SeedAsync(accountingDb);

    // 1.b) تعبئة NameEn لكل الحسابات/أنواع السندات/الصناديق التي أُدخلت
    //      قبل دعم اللغة الإنجليزية — حتى تعمل واجهة EN بدون نصوص عربية متناثرة.
    await NameEnBackfill.RunAsync(accountingDb);

    // 2) مزامنة الصلاحيات الديناميكية لأنواع السندات + هجرة legacy + Bootstrap كامل.
    //    تُنفّذ قبل AuthSeeder حتى يربط الـ seeder الأدوار الافتراضية بأكواد الصلاحيات
    //    الديناميكية الموجودة بالفعل في جدول Permissions.
    var vtSync = sp.GetRequiredService<IVoucherTypePermissionsSync>();
    await vtSync.SyncAsync();

    // 3) Seed الأدوار والمستخدم الأول مع تمرير voucher type codes لربطها بالأدوار
    var voucherCodes = await accountingDb.JournalVoucherTypes
        .AsNoTracking().Select(t => t.Code).ToListAsync();
    await AuthSeeder.SeedAsync(authDb, builder.Configuration, voucherCodes);
    await UnitsOfMeasureSeeder.SeedAsync(inventoryDb);
    await DefaultWarehouseSeeder.SeedAsync(inventoryDb);

    // 4) Bootstrap لجداول الترخيص في schema 'sys' (لا تحتاج DbContext)
    await LicenseBootstrapper.EnsureCreatedAsync(sp, builder.Configuration);

    Log.Information("Database migrations and seed completed successfully.");
}
catch (Exception ex)
{
    // Log the error but allow the app to start — /health and /swagger still work
    // The API endpoints will fail until the DB is reachable
    Log.Error(ex, "Database migration/seed failed at startup. Connection: {Conn}",
        builder.Configuration.GetConnectionString("DefaultConnection")?
            .Split(';').FirstOrDefault() ?? "unknown");
}

// Middleware pipeline
var exposeSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("App:ExposeSwagger", false);
if (exposeSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
// HTTPS redirect: يعمل فقط في Development — في Production يدير IIS الـ SSL
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
var corsPolicy = allowedOrigins is { Length: > 0 } ? "AllowFrontends" : "AllowAll";
app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// ‎حجب الـ API بعد المصادقة وقبل الـ controllers — كي يبقى من تسجيل الدخول
// ‎مفتوحاً وكي نسمح فقط بـ /api/license/* وما يلزم لتطبيق شفرة جديدة.
app.UseMiddleware<LicenseEnforcementMiddleware>();

app.MapControllers();

// الجذر كان بدون مسار فيظهر للمستخدم "لا شيء" أو 404 على IIS — هذا للتأكد أن التطبيق يعمل
app.MapGet("/", () => Results.Text(
    "IraqiTradeCenter Company API — running.\n" +
    "Swagger: /swagger (requires App:ExposeSwagger=true in Production, or Development).\n" +
    "REST: /api/...\n",
    "text/plain; charset=utf-8"));
app.MapGet("/health", () => Results.Text("ok", "text/plain"));

await app.RunAsync();
