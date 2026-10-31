using System.Collections.Concurrent;
using System.Diagnostics;

namespace DefenderObserver.Core.Services;

public sealed class ProcessTracker
{
    private readonly ConcurrentDictionary<int, string> _pidToProcess = new();

    public ProcessTracker()
    {
        // Pre-populate with running processes
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    _pidToProcess[proc.Id] = proc.ProcessName;
                }
                catch
                {
                    // Ignore inaccessible processes
                }
            }
        }
        catch
        {
            // Ignore if process listing fails
        }
    }

    public string? GetProcessName(int pid)
    {
        if (pid <= 0) return null;

        if (_pidToProcess.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        try
        {
            using var proc = Process.GetProcessById(pid);
            var name = proc.ProcessName;
            _pidToProcess[pid] = name;
            return name;
        }
        catch
        {
            return null;
        }
    }

    public void RecordProcess(int pid, string imagePathOrName)
    {
        if (pid <= 0 || string.IsNullOrWhiteSpace(imagePathOrName)) return;

        var clean = Path.GetFileNameWithoutExtension(imagePathOrName.Trim());
        if (!string.IsNullOrEmpty(clean))
        {
            _pidToProcess[pid] = clean;
        }
    }
}
