using Spectre.Console.Cli;
using DefenderObserver.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("DefenderObserver");

    config.AddCommand<CaptureCommand>("capture")
        .WithDescription("Capture Microsoft Defender ETW events in realtime (requires Administrator).");

    config.AddCommand<ReplayCommand>("replay")
        .WithDescription("Replay and filter recorded ETW events from a JSON Lines (.jsonl) file.");

    config.AddCommand<ExplainCommand>("explain")
        .WithDescription("Look up event descriptions, tasks, fields, and enum value maps.");
});

return app.Run(args);
