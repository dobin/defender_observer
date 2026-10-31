using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using DefenderObserver.Core.Services;

namespace DefenderObserver.Cli;

public sealed class ReplaySettings : CommandSettings
{
    [Description("Input JSON Lines file path (.jsonl) to replay.")]
    [CommandArgument(0, "<INPUT>")]
    public string InputPath { get; set; } = string.Empty;

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

public sealed class ReplayCommand : AsyncCommand<ReplaySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ReplaySettings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.InputPath))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] File not found: [yellow]{settings.InputPath}[/]");
            return 1;
        }

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

        AnsiConsole.Write(new Rule($"[bold green]Replaying:[/] [yellow]{settings.InputPath}[/]").LeftJustified());

        int totalRead = 0;
        int matched = 0;

        await foreach (var evt in JsonlRecorder.ReadEventsAsync(settings.InputPath))
        {
            totalRead++;
            normalizer.EnrichLoadedEvent(evt);

            if (filter.IsMatch(evt))
            {
                matched++;
                renderer.Render(evt);
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Replay complete. Matched [bold cyan]{matched}[/] of [bold]{totalRead}[/] recorded events.[/]");
        return 0;
    }
}
