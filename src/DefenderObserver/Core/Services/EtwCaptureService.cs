using Microsoft.Diagnostics.Tracing.Session;
using DefenderObserver.Core.Models;
using DefenderObserver.Core.Providers;

namespace DefenderObserver.Core.Services;

public sealed class EtwCaptureService : IDisposable
{
    private readonly List<IEtwProviderDefinition> _providers;
    private readonly EventNormalizer _normalizer;
    private readonly EventFilter _filter;
    private readonly Action<ObservedEvent> _onEvent;
    private TraceEventSession? _session;
    private readonly string _sessionName;

    public EtwCaptureService(
        IEnumerable<IEtwProviderDefinition> providers,
        EventNormalizer normalizer,
        EventFilter filter,
        Action<ObservedEvent> onEvent,
        string? sessionName = null)
    {
        _providers = providers.ToList();
        _normalizer = normalizer;
        _filter = filter;
        _onEvent = onEvent;
        _sessionName = sessionName ?? $"DefenderObserver_{Guid.NewGuid():N}";
    }

    public void Start(CancellationToken cancellationToken)
    {
        // Check for admin privileges (required for ETW real-time sessions)
        if (!TraceEventSession.IsElevated() ?? false)
        {
            throw new UnauthorizedAccessException("Administrative privileges are required to start a real-time ETW trace session. Please run as Administrator.");
        }

        using var session = new TraceEventSession(_sessionName);
        _session = session;
        session.StopOnDispose = true;

        // Register cancellation callback
        using var reg = cancellationToken.Register(() =>
        {
            try
            {
                session.Stop();
            }
            catch
            {
                // Ignore errors during stop
            }
        });

        // Enable each configured ETW provider
        foreach (var p in _providers)
        {
            session.EnableProvider(p.Guid, p.Level, p.MatchAnyKeywords);
        }

        // Subscribe to dynamic events
        session.Source.Dynamic.All += raw =>
        {
            try
            {
                var obs = _normalizer.Normalize(raw);
                if (_filter.IsMatch(obs))
                {
                    _onEvent(obs);
                }
            }
            catch
            {
                // Never crash the ETW dispatch thread
            }
        };

        // Also catch unhandled/raw events
        session.Source.UnhandledEvents += raw =>
        {
            try
            {
                var obs = _normalizer.Normalize(raw);
                if (_filter.IsMatch(obs))
                {
                    _onEvent(obs);
                }
            }
            catch
            {
            }
        };

        // Process real-time events until stopped or cancelled
        session.Source.Process();
    }

    public void Dispose()
    {
        try
        {
            _session?.Dispose();
        }
        catch
        {
        }
    }
}
