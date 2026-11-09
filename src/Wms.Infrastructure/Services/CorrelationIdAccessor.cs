using Microsoft.AspNetCore.Http;
using Wms.Application.Common.Interfaces;

namespace Wms.Infrastructure.Services;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CorrelationId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return Guid.NewGuid().ToString("N");
            }

            if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId.ToString();
            }

            return context.TraceIdentifier;
        }
    }
}
