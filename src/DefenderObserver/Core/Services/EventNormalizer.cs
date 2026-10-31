using System.Text;
using Microsoft.Diagnostics.Tracing;
using DefenderObserver.Core.Models;

namespace DefenderObserver.Core.Services;

public sealed class EventNormalizer
{
    private readonly KnowledgeBase _kb;
    private readonly ProcessTracker _processTracker;

    public EventNormalizer(KnowledgeBase kb, ProcessTracker processTracker)
    {
        _kb = kb;
        _processTracker = processTracker;
    }

    public ObservedEvent Normalize(TraceEvent rawEvent)
    {
        var providerGuid = rawEvent.ProviderGuid.ToString("D").ToUpperInvariant();
        var eventId = (int)rawEvent.ID;
        var version = (int)rawEvent.Version;

        var meta = _kb.FindEvent(providerGuid, eventId, version);

        var obs = new ObservedEvent
        {
            Timestamp = rawEvent.TimeStamp.ToUniversalTime(),
            ProviderName = string.IsNullOrEmpty(rawEvent.ProviderName) ? (meta?.Task ?? "Unknown") : rawEvent.ProviderName,
            ProviderGuid = providerGuid,
            Id = eventId,
            Version = version,
            Opcode = rawEvent.OpcodeName ?? meta?.Opcode ?? string.Empty,
            Task = rawEvent.TaskName ?? meta?.Task ?? string.Empty,
            Symbol = meta?.Symbol ?? $"{rawEvent.TaskName}_{eventId}",
            Summary = meta?.summaryOrDefault(rawEvent) ?? string.Empty,
            ProcessId = rawEvent.ProcessID,
            ProcessName = rawEvent.ProcessName,
            ThreadId = rawEvent.ThreadID
        };

        if (string.IsNullOrEmpty(obs.ProcessName) && obs.ProcessId > 0)
        {
            obs.ProcessName = _processTracker.GetProcessName(obs.ProcessId);
        }

        // Extract payload
        var payloadNames = rawEvent.PayloadNames;
        if (payloadNames != null && payloadNames.Length > 0)
        {
            for (int i = 0; i < payloadNames.Length; i++)
            {
                var name = payloadNames[i];
                var val = rawEvent.PayloadValue(i);
                obs.Payload[name] = val;
            }
        }
        else if (meta != null && meta.Fields.Count > 0 && rawEvent.EventDataLength > 0)
        {
            // Binary fallback decoding using field definitions from knowledge base
            DecodeBinaryPayload(rawEvent.EventData(), meta.Fields, obs.Payload);
        }
        else if (rawEvent.EventDataLength > 0)
        {
            obs.RawHex = Convert.ToHexString(rawEvent.EventData());
        }

        // Enrich process tracker from relevant BM events
        EnrichProcessTracker(obs);

        // Map known enum values to human labels
        EnrichEnumFields(obs);

        return obs;
    }

    public void EnrichLoadedEvent(ObservedEvent obs)
    {
        // Enrich process name if missing
        if (string.IsNullOrEmpty(obs.ProcessName) && obs.ProcessId > 0)
        {
            obs.ProcessName = _processTracker.GetProcessName(obs.ProcessId);
        }

        // Check if metadata exists in KB
        var meta = _kb.FindEvent(obs.ProviderGuid, obs.Id, obs.Version);
        if (meta != null)
        {
            if (string.IsNullOrEmpty(obs.Summary)) obs.Summary = meta.Summary;
            if (string.IsNullOrEmpty(obs.Symbol)) obs.Symbol = meta.Symbol;
            if (string.IsNullOrEmpty(obs.Task)) obs.Task = meta.Task;
        }

        EnrichProcessTracker(obs);
        EnrichEnumFields(obs);
    }

    private void EnrichProcessTracker(ObservedEvent obs)
    {
        // Check for PID and ImagePath in payload (e.g. BmProcessStart, BmProcessCreate, SmsScan, etc.)
        int pid = obs.ProcessId;
        if (obs.Payload.TryGetValue("PID", out var pidVal) && pidVal is int or long or uint)
        {
            pid = Convert.ToInt32(pidVal);
        }

        string? img = null;
        if (obs.Payload.TryGetValue("ImagePath", out var ip) && ip is string sIp) img = sIp;
        else if (obs.Payload.TryGetValue("ImageName", out var iname) && iname is string sIname) img = sIname;
        else if (obs.Payload.TryGetValue("ProcessPath", out var pp) && pp is string sPp) img = sPp;
        else if (obs.Payload.TryGetValue("Process", out var p) && p is string sP) img = sP;

        if (pid > 0 && !string.IsNullOrEmpty(img))
        {
            _processTracker.RecordProcess(pid, img);
            if (obs.ProcessId == pid && string.IsNullOrEmpty(obs.ProcessName))
            {
                obs.ProcessName = Path.GetFileNameWithoutExtension(img);
            }
        }
    }

    private void EnrichEnumFields(ObservedEvent obs)
    {
        // Enrich known SMS fields
        EnrichSingleField(obs, "Level", "SmsScan.MonitoringLevel");
        EnrichSingleField(obs, "EffectiveLevel", "SmsScan.MonitoringLevel");
        EnrichSingleField(obs, "Origin", "SmsScan.MonitoringRequestOrigin");
        EnrichSingleField(obs, "StopReason", "SmsScan.MonitoringStopReason");
        EnrichSingleField(obs, "ScanReason", "SmsScan.ScanReason");
        EnrichSingleField(obs, "ScanResult", "SmsScan.ScanResult");
    }

    private void EnrichSingleField(ObservedEvent obs, string fieldName, string valueMapName)
    {
        if (obs.Payload.TryGetValue(fieldName, out var val) && val != null)
        {
            var keyStr = val.ToString()!;
            var label = _kb.ResolveValueMap(valueMapName, keyStr);
            if (label != null)
            {
                obs.Payload[$"{fieldName}_Label"] = label;
            }
        }
    }

    private static void DecodeBinaryPayload(byte[] data, List<FieldMetadata> fields, Dictionary<string, object?> target)
    {
        try
        {
            int offset = 0;
            foreach (var f in fields)
            {
                if (offset >= data.Length) break;

                var type = f.Type.Trim();
                if (type.Equals("UInt32", StringComparison.OrdinalIgnoreCase) || type.Equals("HexInt32", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 4 <= data.Length)
                    {
                        uint u = BitConverter.ToUInt32(data, offset);
                        target[f.Name] = type.StartsWith("Hex", StringComparison.OrdinalIgnoreCase) ? $"0x{u:X8}" : u;
                        offset += 4;
                    }
                }
                else if (type.Equals("Int32", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 4 <= data.Length)
                    {
                        target[f.Name] = BitConverter.ToInt32(data, offset);
                        offset += 4;
                    }
                }
                else if (type.Equals("UInt64", StringComparison.OrdinalIgnoreCase) || type.Equals("HexInt64", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 8 <= data.Length)
                    {
                        ulong u = BitConverter.ToUInt64(data, offset);
                        target[f.Name] = type.StartsWith("Hex", StringComparison.OrdinalIgnoreCase) ? $"0x{u:X16}" : u;
                        offset += 8;
                    }
                }
                else if (type.Equals("UInt8", StringComparison.OrdinalIgnoreCase) || type.Equals("Int8", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 1 <= data.Length)
                    {
                        target[f.Name] = data[offset];
                        offset += 1;
                    }
                }
                else if (type.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 4 <= data.Length)
                    {
                        target[f.Name] = BitConverter.ToInt32(data, offset) != 0;
                        offset += 4;
                    }
                    else if (offset + 1 <= data.Length)
                    {
                        target[f.Name] = data[offset] != 0;
                        offset += 1;
                    }
                }
                else if (type.Equals("Pointer", StringComparison.OrdinalIgnoreCase))
                {
                    // Assume 64-bit pointer
                    if (offset + 8 <= data.Length)
                    {
                        ulong ptr = BitConverter.ToUInt64(data, offset);
                        target[f.Name] = $"0x{ptr:X16}";
                        offset += 8;
                    }
                }
                else if (type.Equals("GUID", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 16 <= data.Length)
                    {
                        var guidBytes = new byte[16];
                        Array.Copy(data, offset, guidBytes, 0, 16);
                        target[f.Name] = new Guid(guidBytes).ToString("B");
                        offset += 16;
                    }
                }
                else if (type.Equals("FILETIME", StringComparison.OrdinalIgnoreCase))
                {
                    if (offset + 8 <= data.Length)
                    {
                        long ft = BitConverter.ToInt64(data, offset);
                        try
                        {
                            target[f.Name] = DateTimeOffset.FromFileTime(ft).ToString("O");
                        }
                        catch
                        {
                            target[f.Name] = ft;
                        }
                        offset += 8;
                    }
                }
                else if (type.Equals("UnicodeString", StringComparison.OrdinalIgnoreCase))
                {
                    // Read null-terminated UTF-16LE string
                    int strStart = offset;
                    while (offset + 1 < data.Length && !(data[offset] == 0 && data[offset + 1] == 0))
                    {
                        offset += 2;
                    }
                    int byteLen = offset - strStart;
                    target[f.Name] = Encoding.Unicode.GetString(data, strStart, byteLen);
                    offset += 2; // skip null terminator
                }
                else if (type.Equals("AnsiString", StringComparison.OrdinalIgnoreCase))
                {
                    // Read null-terminated ASCII string
                    int strStart = offset;
                    while (offset < data.Length && data[offset] != 0)
                    {
                        offset++;
                    }
                    int byteLen = offset - strStart;
                    target[f.Name] = Encoding.ASCII.GetString(data, strStart, byteLen);
                    offset++; // skip null terminator
                }
                else if (type.StartsWith("UInt8[", StringComparison.OrdinalIgnoreCase))
                {
                    // Fixed byte array like UInt8[16]
                    var match = System.Text.RegularExpressions.Regex.Match(type, @"\d+");
                    int len = match.Success ? int.Parse(match.Value) : 16;
                    if (offset + len <= data.Length)
                    {
                        var slice = new byte[len];
                        Array.Copy(data, offset, slice, 0, len);
                        target[f.Name] = Convert.ToHexString(slice);
                        offset += len;
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch
        {
            // Best effort binary decode
        }
    }
}

internal static class EventMetadataExtensions
{
    public static string summaryOrDefault(this EventMetadata meta, TraceEvent rawEvent)
    {
        if (!string.IsNullOrEmpty(meta.Summary)) return meta.Summary;
        return $"{meta.Task} {meta.Symbol}";
    }
}
