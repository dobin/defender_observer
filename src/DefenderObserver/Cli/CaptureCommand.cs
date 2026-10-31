using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using DefenderObserver.Core.Providers;
using DefenderObserver.Core.Services;

namespace DefenderObserver.Cli;

public sealed class CaptureSettings : CommandSettings
{
    [Description("Output file path to record events as JSON Lines (.jsonl).")]
    [CommandOption("-o|--output <PATH>")]
    public string? OutputPath { get; set; }

    [Description("Filter by process name or PID (substring or regex).")]
    [CommandOption("-p|--process <PATTERN>")]
    public string? ProcessPattern { get; set; }

    [Description("Filter by file, image, or registry path (substring, wildcard, or regex).")]
    [CommandOption("--path <PATTERN>")]
    public string? PathPattern { get; set; }

    [Description("Filter by Event ID(s), comma-separated (e.g. 8,53,63,74).")]
    [CommandOption("--event-id <IDS>")]
    public string? EventIds { get; set; }

    [Description("Exclude specific Event ID(s), comma-separated (e.g. 38,39).")]
    [CommandOption("--exclude-id <IDS>")]
    public string? ExcludeIds { get; set; }

    [Description("Filter by Task name(s), comma-separated (e.g. BehaviorMonitor,SmsScan).")]
    [CommandOption("-t|--task <TASKS>")]
    public string? Tasks { get; set; }

    [Description("Full text search across all payload fields and summary.")]
    [CommandOption("-s|--search <TEXT>")]
    public string? SearchText { get; set; }

    [Description("Hide the explanation help text under each event.")]
    [CommandOption("--no-help")]
    public bool NoHelp { get; set; }

    [Description("Show verbose/untruncated field values.")]
    [CommandOption("-v|--verbose")]
    public bool Verbose { get; set; }

    [Description("Optional path to knowledge catalog directory (defaults to application directory).")]
    [CommandOption("--knowledge-dir <DIR>")]
    public string? KnowledgeDir { get; set; }
}

public sealed class CaptureCommand : Command<CaptureSettings>
{
    public override int Execute(CommandContext context, CaptureSettings settings, CancellationToken cancellationToken)
    {
        var kb = new KnowledgeBase();
        var knowledgePath = settings.KnowledgeDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Knowledge");
        kb.LoadFromDirectory(knowledgePath);

        var filterOptions = new EventFilterOptions
        {
            ProcessPattern = settings.ProcessPattern,
            PathPattern = settings.PathPattern,
            SearchText = settings.SearchText
        };

        if (!string.IsNullOrWhiteSpace(settings.EventIds))
        {
            foreach (var part in settings.EventIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out int id)) filterOptions.EventIds.Add(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.ExcludeIds))
        {
            foreach (var part in settings.ExcludeIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out int id)) filterOptions.ExcludeEventIds.Add(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.Tasks))
        {
            foreach (var part in settings.Tasks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                filterOptions.Tasks.Add(part);
            }
        }

        var filter = new EventFilter(filterOptions);
        var tracker = new ProcessTracker();
        var normalizer = new EventNormalizer(kb, tracker);
        var renderer = new ConsoleRenderer(showHelp: !settings.NoHelp, verbose: settings.Verbose);

        // Header info
        AnsiConsole.Write(new Rule("[bold green]DefenderObserver - Realtime ETW Capture[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Provider:[/] [bold]Microsoft-Antimalware-Engine[/] [dim]({0A002690-3839-4E3A-B3B6-96D8DF868D99})[/]");
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            AnsiConsole.MarkupLine($"[grey]Recording to:[/] [bold yellow]{settings.OutputPath}[/]");
        }
        if (!string.IsNullOrWhiteSpace(settings.ProcessPattern)) AnsiConsole.MarkupLine($"[grey]Process filter:[/] [cyan]{settings.ProcessPattern}[/]");
        if (!string.IsNullOrWhiteSpace(settings.PathPattern)) AnsiConsole.MarkupLine($"[grey]Path filter:[/] [cyan]{settings.PathPattern}[/]");
        if (filterOptions.EventIds.Count > 0) AnsiConsole.MarkupLine($"[grey]Event IDs:[/] [cyan]{string.Join(", ", filterOptions.EventIds)}[/]");
        if (filterOptions.Tasks.Count > 0) AnsiConsole.MarkupLine($"[grey]Tasks:[/] [cyan]{string.Join(", ", filterOptions.Tasks)}[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop capture...[/]\n");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            AnsiConsole.MarkupLine("\n[bold yellow]Stopping ETW session...[/]");
        };

        var recorder = !string.IsNullOrWhiteSpace(settings.OutputPath)
            ? new JsonlRecorder(settings.OutputPath)
            : null;

        try
        {
            var provider = new AntimalwareEngineProvider();
            using var captureService = new EtwCaptureService(
                [provider],
                normalizer,
                filter,
                evt =>
                {
                    renderer.Render(evt);
                    recorder?.WriteEventAsync(evt).GetAwaiter().GetResult();
                });

            captureService.Start(cts.Token);
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error starting ETW trace:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            recorder?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        AnsiConsole.MarkupLine("[green]Trace completed successfully.[/]");
        return 0;
    }
}
