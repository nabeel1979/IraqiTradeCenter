using System.Net;
using System.Text.Json;
using FluentValidation;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ غير متوقع");
            await HandleAsync(ctx, ex);
        }
    }

    private static Task HandleAsync(HttpContext ctx, Exception ex)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var (code, errors) = ex switch
        {
            ValidationException ve => (HttpStatusCode.BadRequest, ve.Errors.Select(e => e.ErrorMessage).ToList()),
            NotFoundException nf => (HttpStatusCode.NotFound, new List<string> { nf.Message }),
            UnbalancedJournalEntryException ub => (HttpStatusCode.BadRequest, new List<string> { ub.Message }),
            ClosedPeriodException cp => (HttpStatusCode.BadRequest, new List<string> { cp.Message }),
            InsufficientStockException ins => (HttpStatusCode.BadRequest, new List<string> { ins.Message }),
            DomainException de => (HttpStatusCode.BadRequest, new List<string> { de.Message }),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, new List<string> { "غير مصرح" }),
            _ => (HttpStatusCode.InternalServerError, new List<string> { "حصل خطأ في الخادم" })
        };
        ctx.Response.StatusCode = (int)code;
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, errors }));
    }
}
