using System.Net.Http;
using GitHub.Copilot;

namespace OneWare.Copilot.Services;

/// <summary>
/// Ensures every outbound LLM request carries an Idempotency-Key header. Per-turn
/// <c>RequestHeaders</c> are only applied to the main agent, so requests issued by subagents
/// would otherwise go out without one. Missing keys fall back to the key of the current user turn.
/// </summary>
internal sealed class IdempotencyKeyRequestHandler(string headerName, Func<string> currentKeyProvider)
    : CopilotRequestHandler
{
    protected override Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request,
        CopilotRequestContext ctx)
    {
        if (!request.Headers.Contains(headerName))
            request.Headers.TryAddWithoutValidation(headerName, currentKeyProvider());

        return base.SendRequestAsync(request, ctx);
    }

    protected override Task<CopilotWebSocketHandler> OpenWebSocketAsync(CopilotRequestContext ctx)
    {
        if (ctx.Headers.Keys.Any(x => string.Equals(x, headerName, StringComparison.OrdinalIgnoreCase)))
            return base.OpenWebSocketAsync(ctx);

        var headers = new Dictionary<string, IReadOnlyList<string>>(ctx.Headers, StringComparer.OrdinalIgnoreCase)
        {
            [headerName] = [currentKeyProvider()]
        };
        return base.OpenWebSocketAsync(new CopilotRequestContext(ctx) { Headers = headers });
    }
}
