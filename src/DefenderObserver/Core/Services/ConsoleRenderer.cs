using Spectre.Console;
using DefenderObserver.Core.Models;

namespace DefenderObserver.Core.Services;

public sealed class ConsoleRenderer
{
    private readonly bool _showHelp;
    private readonly bool _verbose;

    public ConsoleRenderer(bool showHelp = true, bool verbose = false)
    {
        _showHelp = showHelp;
        _verbose = verbose;
    }

    public void Render(ObservedEvent evt)
    {
        var timeStr = evt.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        var procInfo = !string.IsNullOrEmpty(evt.ProcessName)
            ? $"{evt.ProcessName}:{evt.ProcessId}"
            : evt.ProcessId.ToString();

        // Color coding based on event severity or task
        var symbolMarkup = GetSymbolMarkup(evt);
        var taskMarkup = $"[grey]({Markup.Escape(evt.Task)})[/]";

        AnsiConsole.Markup($"[dim]{timeStr}[/] [bold blue][[{procInfo}]][/] {symbolMarkup} {taskMarkup} [dim]ID:{evt.Id}.{evt.Version}[/]");
        AnsiConsole.WriteLine();

        // Print key payload elements
        if (evt.Payload.Count > 0)
        {
            var parts = new List<string>();
            foreach (var (k, v) in evt.Payload)
            {
                if (v == null) continue;
                if (k.EndsWith("_Label", StringComparison.OrdinalIgnoreCase)) continue;

                var displayVal = v.ToString() ?? "";
                if (evt.Payload.TryGetValue($"{k}_Label", out var labelVal) && labelVal != null)
                {
                    displayVal = $"{displayVal} [bold yellow]({Markup.Escape(labelVal.ToString()!)})[/]";
                }
                else
                {
                    displayVal = Markup.Escape(displayVal);
                }

                // Truncate overly long values unless verbose
                if (!_verbose && displayVal.Length > 90)
                {
                    displayVal = displayVal[..87] + "...";
                }

                parts.Add($"[cyan]{Markup.Escape(k)}[/]={displayVal}");
            }

            if (parts.Count > 0)
            {
                AnsiConsole.MarkupLine($"   [grey]↳[/] {string.Join("  ", parts)}");
            }
        }
        else if (!string.IsNullOrEmpty(evt.RawHex))
        {
            var hexDisplay = evt.RawHex.Length > 64 && !_verbose ? evt.RawHex[..64] + "..." : evt.RawHex;
            AnsiConsole.MarkupLine($"   [grey]↳ RawHex=[/]{hexDisplay}");
        }

        // Print help text / explanation if enabled and available
        if (_showHelp && !string.IsNullOrWhiteSpace(evt.Summary))
        {
            AnsiConsole.MarkupLine($"   [dim green]ℹ {Markup.Escape(evt.Summary)}[/]");
        }
    }

    private static string GetSymbolMarkup(ObservedEvent evt)
    {
        var name = Markup.Escape(string.IsNullOrEmpty(evt.Symbol) ? $"Event_{evt.Id}" : evt.Symbol);

        // Highlight high-value security events
        if (evt.Id == 74) // SenseRemediation
        {
            return $"[bold red]🚨 {name}[/]";
        }
        if (evt.Id == 8 || evt.Id == 27) // BmDetection or BmNetworkDetection
        {
            return $"[bold red]⚠ {name}[/]";
        }
        if (evt.Id == 63) // BmProcessTainting
        {
            return $"[bold yellow]☣ {name}[/]";
        }
        if (evt.Id == 53) // BmOpenProcess
        {
            return $"[yellow]🔍 {name}[/]";
        }
        if (evt.Id == 29) // BmRemoteThreadCreate
        {
            return $"[bold purple]💉 {name}[/]";
        }
        if (evt.Id == 7) // SkippedFile
        {
            return $"[magenta]⏭ {name}[/]";
        }
        if (evt.Id == 108) // SenseExclusion
        {
            return $"[bold magenta]🛡 {name}[/]";
        }
        if (evt.Id == 95) // BmFileOverwrite
        {
            return $"[darkorange]📝 {name}[/]";
        }
        if (evt.Task.Equals("SmsScan", StringComparison.OrdinalIgnoreCase))
        {
            return $"[cyan]🧠 {name}[/]";
        }

        return $"[white]{name}[/]";
    }
}
