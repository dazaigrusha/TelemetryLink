using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Cli.Commands;

/// <summary>
/// Порівняння алгоритмів — головна команда для експериментальної частини звіту.
///
/// Міняється РІВНО ОДИН компонент тракту, решта лишається незмінною. Це пряма
/// вимога методики: один експеримент — одна зміна. Інакше неможливо сказати,
/// що саме дало різницю.
/// </summary>
public static class CompareCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        var session = context.LoadSession();
        string[] scenarios = options.List("scenario", "calm", "working", "emergency");

        if (options.Has("ablation")) return Ablation(context, session, scenarios, options);

        // --vary packer=dp,greedy
        string vary = options.Value("vary", "packer=dp,greedy");
        var parts = vary.Split('=', 2);
        string dimension = parts[0].Trim();
        string[] variants = parts.Length > 1
            ? parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : ["dp", "greedy"];

        var reports = new List<SessionReport>();
        foreach (string scenarioName in scenarios)
        {
            var scenario = context.Config.Scenario(scenarioName);
            foreach (string variant in variants)
            {
                var pipeline = BuildVariant(context, dimension, variant);
                reports.Add(new SessionRunner(context.Config, pipeline)
                    .Run(session, scenario, context.ChannelSeed));
            }
        }

        ReportRenderer.PrintComparison(dimension, $"{dimension} ∈ {{{string.Join(", ", variants)}}}", reports);
        ReportRenderer.WriteCsv(options.Value("out", $"results/compare_{dimension}.csv"), reports);
        return 0;
    }

    private static PipelineOptions BuildVariant(CommandContext context, string dimension, string variant)
        => dimension switch
        {
            "codec" => context.BuildPipeline(codec: variant),
            "packer" => context.BuildPipeline(packer: variant),
            "scheduler" => context.BuildPipeline(scheduler: variant),
            "classifier" => context.BuildPipeline(classifier: variant),
            _ => throw new ArgumentException(
                $"Невідомий вимір '{dimension}'. Доступні: codec, packer, scheduler, classifier.")
        };

    /// <summary>
    /// Таблиця 2 звіту: Q повної системи і Q системи з вимкненим по черзі
    /// кожним блоком. Нульовий виграш — теж результат, і його треба пояснити,
    /// а не сховати.
    /// </summary>
    private static int Ablation(
        CommandContext context,
        Core.InputOutput.TelemetrySession session,
        string[] scenarios,
        CommandLineOptions options)
    {
        (string Label, PipelineOptions Pipeline)[] variants =
        [
            ("повна система", context.BuildPipeline()),
            ("без стиснення", context.BuildPipeline(codec: "none")),
            ("без ДП (жадібно)", context.BuildPipeline(packer: "greedy")),
            ("без планувальника", context.BuildPipeline(scheduler: "sequential")),
            ("без класифікатора", context.BuildPipeline(classifier: "majority"))
        ];

        Console.WriteLine();
        Console.WriteLine("вплив кожного блоку: Q повної системи і з вимкненим блоком");
        Console.WriteLine();
        Console.WriteLine($"{"варіант",-22}" + string.Join("", scenarios.Select(s => $"{s,18}")));
        Console.WriteLine(new string('-', 22 + 18 * scenarios.Length));

        var all = new List<SessionReport>();
        var baseline = new Dictionary<string, double>();

        foreach (var (label, pipeline) in variants)
        {
            var line = new List<string>();
            foreach (string scenarioName in scenarios)
            {
                var report = new SessionRunner(context.Config, pipeline)
                    .Run(session, context.Config.Scenario(scenarioName), context.ChannelSeed);
                all.Add(report);

                if (label == "повна система") baseline[scenarioName] = report.Q;

                string delta = label == "повна система"
                    ? ""
                    : $" ({report.Q - baseline[scenarioName]:+0.000;-0.000;0.000})";
                line.Add($"{report.Q:F4}{delta}");
            }

            Console.WriteLine($"{label,-22}" + string.Join("", line.Select(v => $"{v,18}")));
        }

        Console.WriteLine();
        ReportRenderer.WriteCsv(options.Value("out", "results/ablation.csv"), all);
        return 0;
    }
}
