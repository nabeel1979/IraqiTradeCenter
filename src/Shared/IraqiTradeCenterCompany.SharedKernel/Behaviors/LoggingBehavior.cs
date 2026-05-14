using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IraqiTradeCenterCompany.SharedKernel.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var name = typeof(TRequest).Name;
        _logger.LogInformation("بدء: {Name}", name);
        try
        {
            var resp = await next();
            sw.Stop();
            _logger.LogInformation("اكتمل: {Name} في {Ms}ms", name, sw.ElapsedMilliseconds);
            return resp;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "فشل: {Name} بعد {Ms}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
