using LeadApp.Options;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;

namespace LeadApp.Services.Leads;

/// <summary>
/// Sliding-window limiter for form submissions per client. Blazor submits travel over the SignalR
/// circuit, so ASP.NET Core's HTTP rate-limiting middleware does not see them.
/// </summary>
public sealed class SubmissionRateLimiter(IOptions<LeadFormOptions> options, TimeProvider time)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _hits = [];

    public bool TryAcquire(string clientKey)
    {
        var now = time.GetUtcNow();
        var window = TimeSpan.FromMinutes(options.Value.WindowMinutes);

        lock (_lock)
        {
            if (_hits.Count > 10_000)
                Prune(now - window);

            if (!_hits.TryGetValue(clientKey, out var queue))
                _hits[clientKey] = queue = new Queue<DateTimeOffset>();

            while (queue.Count > 0 && queue.Peek() <= now - window)
                queue.Dequeue();

            if (queue.Count >= options.Value.SubmissionsPerWindow)
                return false;

            queue.Enqueue(now);
            return true;
        }
    }

    private void Prune(DateTimeOffset cutoff)
    {
        foreach (var key in _hits.Where(kv => kv.Value.All(t => t <= cutoff)).Select(kv => kv.Key).ToList())
            _hits.Remove(key);
    }
}

/// <summary>Per-circuit information about the connected client.</summary>
public sealed class ClientInfo
{
    public string? IpAddress { get; set; }
    public string Key => IpAddress ?? "unknown";
}

/// <summary>Captures the client IP from the SignalR connection when the circuit opens.</summary>
public sealed class ClientInfoCircuitHandler(ClientInfo clientInfo, IHttpContextAccessor accessor) : CircuitHandler
{
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        clientInfo.IpAddress ??= accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        return Task.CompletedTask;
    }
}
