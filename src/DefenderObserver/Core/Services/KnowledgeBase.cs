using System.Text.Json;
using DefenderObserver.Core.Models;

namespace DefenderObserver.Core.Services;

public sealed class KnowledgeBase
{
    private readonly Dictionary<string, ProviderCatalog> _catalogsByGuid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderCatalog> _catalogsByName = new(StringComparer.OrdinalIgnoreCase);
    
    // Quick lookup: key = $"{providerGuid}:{eventId}:{version}" and fallback key = $"{providerGuid}:{eventId}"
    private readonly Dictionary<string, EventMetadata> _eventsByKey = new(StringComparer.OrdinalIgnoreCase);

    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var catalog = JsonSerializer.Deserialize<ProviderCatalog>(json);
                if (catalog != null)
                {
                    RegisterCatalog(catalog);
                }
            }
            catch
            {
                // Silently skip malformed catalog files
            }
        }
    }

    public void RegisterCatalog(ProviderCatalog catalog)
    {
        var normGuid = NormalizeGuid(catalog.ProviderGuid);
        _catalogsByGuid[normGuid] = catalog;
        _catalogsByName[catalog.ProviderName] = catalog;

        foreach (var evt in catalog.Events)
        {
            // Specific key with version
            _eventsByKey[$"{normGuid}:{evt.Id}:{evt.Version}"] = evt;
            // Fallback key without version
            var fallbackKey = $"{normGuid}:{evt.Id}";
            if (!_eventsByKey.ContainsKey(fallbackKey))
            {
                _eventsByKey[fallbackKey] = evt;
            }
        }
    }

    public EventMetadata? FindEvent(string providerGuid, int eventId, int version)
    {
        var norm = NormalizeGuid(providerGuid);
        if (_eventsByKey.TryGetValue($"{norm}:{eventId}:{version}", out var specific))
        {
            return specific;
        }

        if (_eventsByKey.TryGetValue($"{norm}:{eventId}", out var fallback))
        {
            return fallback;
        }

        return null;
    }

    public ProviderCatalog? GetCatalog(string providerGuidOrName)
    {
        var norm = NormalizeGuid(providerGuidOrName);
        if (_catalogsByGuid.TryGetValue(norm, out var catByGuid))
        {
            return catByGuid;
        }

        if (_catalogsByName.TryGetValue(providerGuidOrName, out var catByName))
        {
            return catByName;
        }

        return null;
    }

    public IEnumerable<ProviderCatalog> AllCatalogs => _catalogsByGuid.Values;

    public string? ResolveValueMap(string valueMapName, string key)
    {
        foreach (var cat in _catalogsByGuid.Values)
        {
            if (cat.ValueMaps.TryGetValue(valueMapName, out var map))
            {
                if (map.TryGetValue(key, out var label))
                {
                    return label;
                }
            }
        }
        return null;
    }

    private static string NormalizeGuid(string guidStr)
    {
        if (Guid.TryParse(guidStr, out var parsed))
        {
            return parsed.ToString("D").ToUpperInvariant();
        }
        return guidStr.Trim('{', '}').ToUpperInvariant();
    }
}
