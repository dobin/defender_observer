# DefenderObserver

A C# / .NET 9 console tool for capturing, recording, replaying, and analyzing **Microsoft Defender Antivirus & EDR** ETW telemetry in real-time.

For RedTeamers wanting to observe how Defender reacts to their offensive tooling.

ETW events captured from: 
* `Microsoft-Antimalware-Engine`


Vibe-Coded one-shot with Gemini 4 Argon (plan) and Gemini 3.8 Flash (implementation) based on `docs/antimalware-engine.md`. 


## Example Output


```
> dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- capture -p 32456

09:43:06.443 [MsMpEng:32456] StreamScanRequestStartEvent (Streamscanrequest) ID:5.1
   ↳ Id=195974179  Path=\Device\HarddiskVolume3\Users\dobin\AppData\Roaming\discord\sentry\scope_v3.json  Process=\Device\HarddiskVolume3\Users\dobin\AppData\Local\Discord\app-1.0.9261\Discord.exe  Reason=5 ThreadTime=01/01/1601 01:00:07  PID=31264
   ℹ Starts a scan request for streamed content, with path and process context.

09:43:06.443 [MsMpEng:32456] ⏭ SkippedFileEvent (Skippedfile) ID:7.0
   ↳ Path=\Device\HarddiskVolume3\Users\dobin\AppData\Roaming\discord\sentry\scope_v3.json  Reason=Log skip
   ℹ Records a skipped file and the associated reason.

09:43:06.443 [MsMpEng:32456] StreamScanRequestEndEvent (Streamscanrequest) ID:6.1
   ↳ Id=195974179  Path=\Device\HarddiskVolume3\Users\dobin\AppData\Roaming\discord\sentry\scope_v3.json  Process=\Device\HarddiskVolume3\Users\dobin\AppData\Local\Discord\app-1.0.9261\Discord.exe  Reason=5 ThreadTime=01/01/1601 01:00:07  PID=31264
   ℹ Ends the streamed-content scan request.

09:43:06.472 [MsMpEng:32456] BmNotificationHandleStopEvent (BehaviorMonitoring) ID:112.0
   ↳ PID=17092  AttrId=16435  AttrSeq=0  AttrSubset=0  MatchedThreatsNumber=0  IsMultiProcMatch=False  IsMultiProcDetection=False
   ℹ Ends notification handling and reports match-related counts and flags.

09:43:06.472 [MsMpEng:32456] BmEtwEvent (BehaviorMonitoring) ID:60.0
   ↳ PID=43940  Channel=ThreatIntel  EventId=19
   ℹ ETW-related activity with channel and event identifiers.

09:43:06.472 [MsMpEng:32456] BmNotificationHandleStartEvent (BehaviorMonitoring) ID:111.0
   ↳ PID=17092  AttrId=16435  AttrSeq=0  AttrSubset=0
```


## Features

* **Real-time ETW capture**: Captures all events emitted by the Defender Antivirus and EDR engine using Microsoft `TraceEvent`
* **Streaming JSON Lines Recording**: Record live telemetry directly to `.jsonl` (NDJSON) files
* **Offline Replay**: Replay any `.jsonl` trace with identical filtering, formatting, and automatic enrichment.
* **Integrated Knowledge Base**: Built-in JSON catalog. Every event displays inline human-readable explanation text. 


## Usage

### 1. Real-time Capture

Capturing `Microsoft-Antimalware-Engine` requires administrator privileges. 


```powershell
# Realtime console monitoring with inline event explanations
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- capture

# Capture and record to JSON Lines file while filtering for powershell or cmd
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- capture -o trace.jsonl -p powershell

# Filter for specific high-value detection & injection events
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- capture --event-id 8,53,63,74 -o detections.jsonl
```

Filtering:
  * `--path <pattern>`: Substring, wildcard, or regex matching on all path/file/image/registry fields.
  * `-p|--process <pattern>`: Match process name or PID (auto-resolved from OS and BmProcess events).
  * `--event-id <ids>` & `--exclude-id <ids>`: Target or exclude specific IDs (e.g. `--event-id 8,53,63,74`).
  * `-t|--task <tasks>`: Filter by Defender task (e.g. `-t BehaviorMonitor,SmsScan,SenseRemediation`).
  * `-s|--search <text>`: Full-text search across all payload keys, values, and summaries.


### 2. Replay Recorded Traces

```powershell
# Replay all events with colored output and inline explanations
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- replay trace.jsonl

# Replay with path and task filtering
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- replay trace.jsonl --path mimikatz -t BehaviorMonitor

# Hide explanations for compact view
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- replay trace.jsonl --no-help
```

### 3. Knowledge Base Lookup (`explain`)

```powershell
# Look up explanation, opcode, and fields for SenseRemediation (Event 74)
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- explain 74

# Search for any event mentioning "Tainting" or "RemoteThread"
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- explain Tainting

# List all tasks
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- explain --tasks

# List all enumerated value maps (SMS Scan results, stop reasons, etc.)
dotnet run --project src/DefenderObserver/DefenderObserver.csproj -- explain --enums
```
