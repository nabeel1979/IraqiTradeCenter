using System.Text;
using IraqiTradeCenterCompany.API.Extensions;
using IraqiTradeCenterCompany.API.Middlewares;
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

// 3) MediatR Behaviors المشتركة
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// 4) خدمات مشتركة
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeService, DateTimeService>();

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

// 7) CORS
builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

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
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var accountingDb = sp.GetRequiredService<AccountingDbContext>();
    var inventoryDb = sp.GetRequiredService<InventoryDbContext>();
    var storeDb = sp.GetRequiredService<StoreDbContext>();

    await accountingDb.Database.EnsureCreatedAsync();
    await inventoryDb.Database.EnsureCreatedAsync();
    await storeDb.Database.EnsureCreatedAsync();

    await ChartOfAccountsSeeder.SeedAsync(accountingDb);
    await FiscalYearSeeder.SeedAsync(accountingDb);
    await UnitsOfMeasureSeeder.SeedAsync(inventoryDb);
    await DefaultWarehouseSeeder.SeedAsync(inventoryDb);
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
