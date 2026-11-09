using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Cli.Commands;

/// <summary>Одна зміна алгоритму, однакові вхідні дані та seed, повтори й експорт повних вимірів.</summary>
public static class CompareCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        var settings = ComparisonSettings.Parse(options, context.Config.ChannelSeed);
        var session = context.LoadSession();
        var scenarios = settings.Scenarios.Select(context.Config.Scenario).ToArray();
        var variants = settings.Ablation ? Ablation(context) : settings.Variants
            .Select(name => new ExperimentVariant(name, BuildVariant(context, settings.Dimension, name))).ToArray();
        // Усі варіанти перевіряються ДО першого вимірювання, включно з межами brute.
        if (variants.Any(v => v.Options.Packer.Name == "brute" && v.Options.Tuning.WindowFrames > 20))
            throw new ArgumentException("Для порівняння з brute задайте однакове --window <= 20 всім варіантам.");
        string output = options.Has("out") ? options.Require("out")
            : settings.Ablation ? "results/ablation.csv" : $"results/compare_{settings.Dimension}.csv";
        ExperimentExporter.ValidateDestinations(output,
            options.Value("session", Path.Combine(CommandContext.DataDirectory, "session.csv")),
            options.Value("config", Path.Combine(CommandContext.DataDirectory, "team_config.json")), options.Value("model"));
        Console.WriteLine($"Прогрівів: {settings.Warmup}; повторів: {settings.Repeats}; seed: {string.Join(",", settings.Seeds)}.");
        var runs = ComparisonExperiment.Run(variants, scenarios, settings.Seeds, settings.Repeats, settings.Warmup,
            (variant, scenario, seed) => new SessionRunner(context.Config, variant.Options).Run(session, scenario, seed));
        var summaries = ComparisonExperiment.Summarize(runs, variants[0].Name);
        ExperimentExporter.Print(summaries, variants);
        ExperimentExporter.Write(output, settings, variants, runs, summaries, context.Config, options);
        if (runs.Any(r => !r.Report.ExactRestore)) return 1;
        return options.Has("strict") && runs.Any(r => r.Report.CriticalLossRatio
            > context.Config.CriticalLossThreshold(r.Report.ScenarioName) + 1e-9) ? 1 : 0;
    }

    private static PipelineOptions BuildVariant(CommandContext context, string dimension, string variant)
        => dimension switch
        {
            "codec" => context.BuildPipeline(codec: variant),
            "packer" => context.BuildPipeline(packer: variant),
            "scheduler" => context.BuildPipeline(scheduler: variant),
            "classifier" => context.BuildPipeline(classifier: variant),
            _ => throw new ArgumentException("Невідомий вимір порівняння.")
        };

    private static ExperimentVariant[] Ablation(CommandContext context)
    {
        var baseline = context.BuildPipeline();
        ExperimentVariant Variant(string name, string dimension, string value)
        {
            var changed = BuildVariant(context, dimension, value);
            bool same = dimension switch
            {
                "codec" => baseline.Codec.Name == value,
                "packer" => baseline.Packer.Name == value,
                "scheduler" => baseline.Scheduler.Name == value,
                "classifier" => baseline.Classifier.Name == value,
                _ => false
            };
            return new(name, changed, same, same
                ? "Без зміни конфігурації: використано базові виміри, ΔQ=0; це не окремий експеримент."
                : null);
        }
        return [new("baseline", baseline, Note: "Поточна конфігурація; MLP з'явиться у наступній частині."),
            Variant("no-compression", "codec", "none"), Variant("greedy-packer", "packer", "greedy"),
            Variant("sequential-scheduler", "scheduler", "sequential"), Variant("majority-classifier", "classifier", "majority")];
    }
}
