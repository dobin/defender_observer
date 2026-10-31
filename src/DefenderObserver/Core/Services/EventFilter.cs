using System.Text.RegularExpressions;
using DefenderObserver.Core.Models;

namespace DefenderObserver.Core.Services;

public sealed class EventFilterOptions
{
    public HashSet<int> EventIds { get; set; } = [];
    public HashSet<int> ExcludeEventIds { get; set; } = [];
    public HashSet<string> Tasks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ProcessPattern { get; set; }
    public string? PathPattern { get; set; }
    public string? SearchText { get; set; }
}

public sealed class EventFilter
{
    private static readonly string[] PathFieldNames =
    [
        "FilePath", "FileName", "OldFileName", "ImagePath", "ImageName", "RealPath", "VPath",
        "BasePath", "Path", "FolderName", "TargetResource", "ParentResource", "First Resource Path",
        "KeyPath", "PipeName"
    ];

    private readonly EventFilterOptions _options;
    private readonly Regex? _procRegex;
    private readonly Regex? _pathRegex;

    public EventFilter(EventFilterOptions options)
    {
        _options = options;
        if (!string.IsNullOrWhiteSpace(_options.ProcessPattern))
        {
            _procRegex = CreateRegex(_options.ProcessPattern);
        }
        if (!string.IsNullOrWhiteSpace(_options.PathPattern))
        {
            _pathRegex = CreateRegex(_options.PathPattern);
        }
    }

    public bool IsMatch(ObservedEvent evt)
    {
        // 1. Exclude Event IDs
        if (_options.ExcludeEventIds.Count > 0 && _options.ExcludeEventIds.Contains(evt.Id))
        {
            return false;
        }

        // 2. Include Event IDs
        if (_options.EventIds.Count > 0 && !_options.EventIds.Contains(evt.Id))
        {
            return false;
        }

        // 3. Task filter
        if (_options.Tasks.Count > 0)
        {
            if (string.IsNullOrEmpty(evt.Task) || !_options.Tasks.Contains(evt.Task))
            {
                return false;
            }
        }

        // 4. Process filter (matches PID, ProcessName, or payload PID/image fields)
        if (_procRegex != null)
        {
            if (!MatchesProcess(evt))
            {
                return false;
            }
        }

        // 5. Path filter
        if (_pathRegex != null)
        {
            if (!MatchesPath(evt))
            {
                return false;
            }
        }

        // 6. Full-text search across all payload fields & summary
        if (!string.IsNullOrWhiteSpace(_options.SearchText))
        {
            if (!MatchesSearchText(evt, _options.SearchText))
            {
                return false;
            }
        }

        return true;
    }

    private bool MatchesProcess(ObservedEvent evt)
    {
        // Check event ProcessId and ProcessName
        if (_procRegex!.IsMatch(evt.ProcessId.ToString())) return true;
        if (!string.IsNullOrEmpty(evt.ProcessName) && _procRegex.IsMatch(evt.ProcessName)) return true;

        // Check payload for other PID / process image fields
        foreach (var (k, v) in evt.Payload)
        {
            if (v == null) continue;
            var str = v.ToString()!;
            if (k.Contains("PID", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Image", StringComparison.OrdinalIgnoreCase))
            {
                if (_procRegex.IsMatch(str)) return true;
                // Also check file name without extension if it looks like a path
                var fn = Path.GetFileNameWithoutExtension(str);
                if (!string.IsNullOrEmpty(fn) && _procRegex.IsMatch(fn)) return true;
            }
        }

        return false;
    }

    private bool MatchesPath(ObservedEvent evt)
    {
        foreach (var field in PathFieldNames)
        {
            if (evt.Payload.TryGetValue(field, out var val) && val != null)
            {
                var sVal = val is System.Text.Json.JsonElement je ? je.GetString() : val.ToString();
                if (!string.IsNullOrEmpty(sVal) && _pathRegex!.IsMatch(sVal)) return true;
            }
        }

        // Fallback: check any payload field containing "path" or "name" in key
        foreach (var (k, v) in evt.Payload)
        {
            if (v != null && (k.Contains("Path", StringComparison.OrdinalIgnoreCase) || k.Contains("File", StringComparison.OrdinalIgnoreCase)))
            {
                var sVal = v is System.Text.Json.JsonElement je ? je.GetString() : v.ToString();
                if (!string.IsNullOrEmpty(sVal) && _pathRegex!.IsMatch(sVal)) return true;
            }
        }

        return false;
    }

    private static bool MatchesSearchText(ObservedEvent evt, string search)
    {
        if (evt.Symbol.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (evt.Task.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (evt.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (evt.ProcessName != null && evt.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var (_, v) in evt.Payload)
        {
            if (v != null && v.ToString()!.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Regex CreateRegex(string pattern)
    {
        // If pattern contains regex special chars, compile as regex, otherwise case-insensitive substring
        bool isPlain = !pattern.Any(c => "*?+[](){}^$\\".Contains(c));
        if (isPlain)
        {
            return new Regex(Regex.Escape(pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        // If wildcard (* or ?), convert to regex
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var r = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(r, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
