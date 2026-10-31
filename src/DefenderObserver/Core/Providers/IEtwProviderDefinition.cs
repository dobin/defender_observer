using Microsoft.Diagnostics.Tracing;

namespace DefenderObserver.Core.Providers;

public interface IEtwProviderDefinition
{
    string Name { get; }
    Guid Guid { get; }
    ulong MatchAnyKeywords { get; }
    TraceEventLevel Level { get; }
    string CatalogFileName { get; }
}
