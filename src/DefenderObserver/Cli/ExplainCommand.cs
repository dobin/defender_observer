using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using DefenderObserver.Core.Services;

namespace DefenderObserver.Cli;

public sealed class ExplainSettings : CommandSettings
{
    [Description("Event ID or partial symbol name to explain (e.g. 74, BmDetection, SmsScan).")]
    [CommandArgument(0, "[TARGET]")]
    public string? Target { get; set; }

    [Description("Filter by Task name (e.g. BehaviorMonitor, SmsScan, SenseRemediation).")]
    [CommandOption("-t|--task <TASK>")]
    public string? TaskName { get; set; }

    [Description("List all known tasks.")]
    [CommandOption("--tasks")]
    public bool ListTasks { get; set; }

    [Description("List all known value maps (enums).")]
    [CommandOption("--enums")]
    public bool ListEnums { get; set; }

    [Description("Optional path to knowledge catalog directory.")]
    [CommandOption("--knowledge-dir <DIR>")]
    public string? KnowledgeDir { get; set; }
}

public sealed class ExplainCommand : Command<ExplainSettings>
{
    public override int Execute(CommandContext context, ExplainSettings settings, CancellationToken cancellationToken)
    {
        var kb = new KnowledgeBase();
        var knowledgePath = settings.KnowledgeDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Knowledge");
        kb.LoadFromDirectory(knowledgePath);

        var catalog = kb.AllCatalogs.FirstOrDefault();
        if (catalog == null)
        {
            AnsiConsole.MarkupLine("[bold red]No knowledge catalog found.[/]");
            return 1;
        }

        if (settings.ListTasks)
        {
            AnsiConsole.Write(new Rule("[bold green]Defender ETW Tasks[/]").LeftJustified());
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Task Name");
            table.AddColumn("Manifest Task");
            table.AddColumn("Task GUID");

            foreach (var t in catalog.Tasks)
            {
                table.AddRow($"[cyan]{Markup.Escape(t.Name)}[/]", Markup.Escape(t.ManifestTask), $"[dim]{t.Guid}[/]");
            }
            AnsiConsole.Write(table);
            return 0;
        }

        if (settings.ListEnums)
        {
            AnsiConsole.Write(new Rule("[bold green]Value Maps / Enums[/]").LeftJustified());
            foreach (var (mapName, entries) in catalog.ValueMaps)
            {
                AnsiConsole.MarkupLine($"[bold yellow]{Markup.Escape(mapName)}[/]:");
                foreach (var (k, v) in entries)
                {
                    AnsiConsole.MarkupLine($"  [cyan]{k}[/] -> [white]{Markup.Escape(v)}[/]");
                }
                AnsiConsole.WriteLine();
            }
            return 0;
        }

        // Filter events
        var query = catalog.Events.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(settings.TaskName))
        {
            query = query.Where(e => e.Task.Equals(settings.TaskName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(settings.Target))
        {
            var target = settings.Target.Trim();
            if (int.TryParse(target, out int targetId))
            {
                query = query.Where(e => e.Id == targetId);
            }
            else
            {
                query = query.Where(e =>
                    e.Symbol.Contains(target, StringComparison.OrdinalIgnoreCase) ||
                    e.Task.Contains(target, StringComparison.OrdinalIgnoreCase) ||
                    e.Summary.Contains(target, StringComparison.OrdinalIgnoreCase));
            }
        }

        var results = query.ToList();
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No events found matching query '[bold]{settings.Target ?? settings.TaskName}[/]'.[/]");
            return 0;
        }

        AnsiConsole.Write(new Rule($"[bold green]Event Knowledge Base ({results.Count} matches)[/]").LeftJustified());

        foreach (var evt in results)
        {
            var panel = new Panel(
                new Markup(
                    $"[bold cyan]Task:[/] {Markup.Escape(evt.Task)}   [bold cyan]Opcode:[/] {Markup.Escape(evt.Opcode)}\n" +
                    $"[bold cyan]Summary:[/] [white]{Markup.Escape(evt.Summary)}[/]\n" +
                    (evt.Fields.Count > 0
                        ? $"[bold cyan]Fields:[/] [dim]{string.Join(", ", evt.Fields.Select(f => $"{f.Name}:{f.Type}"))}[/]"
                        : "[dim]No payload fields declared[/]")
                )
            )
            {
                Header = new PanelHeader($"[bold yellow]Event {evt.Id}.{evt.Version} ({Markup.Escape(evt.Symbol)})[/]")
            };
            panel.Border = BoxBorder.Rounded;
            AnsiConsole.Write(panel);
        }

        return 0;
    }
}
