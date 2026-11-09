using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Core.Metrics;

public sealed record ExperimentVariant(string Name, PipelineOptions Options, bool ReuseBaseline = false, string? Note = null);
public sealed record ExperimentRun(string Variant, int ChannelSeed, int Repeat, bool ReusedBaseline, SessionReport Report);
public sealed record ExperimentSummary(string Scenario, string Variant, int Samples, int MeasuredRuns,
    double MeanS, double MeanR, double MeanT, double MeanQ, double StdDevQ, double MeanDeltaQ,
    double MeanElapsedMs, double MeanSentBits, double MeanDeliveredValue, double MeanCriticalLoss,
    bool ExactRestore);

/// <summary>Вимірювання без файлового вводу-виводу: прогрів, повтори, однакові seed і ротація порядку.</summary>
public static class ComparisonExperiment
{
    public static IReadOnlyList<ExperimentRun> Run(IReadOnlyList<ExperimentVariant> variants,
        IReadOnlyList<ScenarioConfig> scenarios, IReadOnlyList<int> seeds, int repeats, int warmup,
        Func<ExperimentVariant, ScenarioConfig, int, SessionReport> evaluate)
    {
        if (variants.Count == 0 || variants[0].ReuseBaseline
            || variants.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variants.Count)
            throw new ArgumentException("Потрібні різні варіанти та справжня базова конфігурація першою.");
        if (scenarios.Count == 0 || scenarios.Select(s => s.Name).Distinct().Count() != scenarios.Count
            || seeds.Count == 0 || seeds.Distinct().Count() != seeds.Count || repeats < 1 || warmup < 0)
            throw new ArgumentException("Некоректні сценарії, seed або кількість повторів.");
        if (variants.Where(v => v.ReuseBaseline).Any(v => v.Options.Describe() != variants[0].Options.Describe()
                || v.Options.Tuning != variants[0].Options.Tuning || v.Options.DeviceId != variants[0].Options.DeviceId))
            throw new ArgumentException("Повторне використання бази допустиме лише для незміненої конфігурації.");
        var measured = variants.Where(v => !v.ReuseBaseline).ToArray();
        var runs = new List<ExperimentRun>();
        foreach (var scenario in scenarios)
            foreach (int seed in seeds)
            {
                for (int i = 0; i < warmup; i++)
                    foreach (var variant in measured)
                        if (!evaluate(variant, scenario, seed).ExactRestore)
                            throw new InvalidDataException("Прогрів виявив порушення точного відновлення.");
                for (int repeat = 1; repeat <= repeats; repeat++)
                {
                    var reports = new Dictionary<string, SessionReport>();
                    // Змінюємо перший варіант кожного повтору, щоб він не отримував
                    // постійно всі початкові витрати JIT/кешів. Це не усуває весь шум.
                    for (int i = 0; i < measured.Length; i++)
                    {
                        var variant = measured[(i + repeat - 1) % measured.Length];
                        reports[variant.Name] = evaluate(variant, scenario, seed);
                    }
                    foreach (var variant in variants)
                    {
                        var report = reports[variant.ReuseBaseline ? variants[0].Name : variant.Name];
                        runs.Add(new ExperimentRun(variant.Name, seed, repeat, variant.ReuseBaseline, report));
                    }
                }
            }
        return runs;
    }

    public static IReadOnlyList<ExperimentSummary> Summarize(IReadOnlyList<ExperimentRun> runs, string baseline)
    {
        var references = runs.Where(r => r.Variant == baseline)
            .ToDictionary(r => (r.Report.ScenarioName, r.ChannelSeed, r.Repeat), r => r.Report.Q);
        return runs.GroupBy(r => (r.Report.ScenarioName, r.Variant)).Select(group =>
        {
            var values = group.ToArray();
            return new ExperimentSummary(group.Key.ScenarioName, group.Key.Variant, values.Length,
                values.Count(r => !r.ReusedBaseline), values.Average(r => r.Report.S),
                values.Average(r => r.Report.R), values.Average(r => r.Report.T), values.Average(r => r.Report.Q),
                SampleStdDev(values.Select(r => r.Report.Q)),
                values.Average(r => r.Report.Q - references[(r.Report.ScenarioName, r.ChannelSeed, r.Repeat)]),
                values.Average(r => r.Report.ElapsedSeconds * 1000), values.Average(r => (double)r.Report.SentBits),
                values.Average(r => (double)r.Report.DeliveredValue), values.Average(r => r.Report.CriticalLossRatio),
                values.All(r => r.Report.ExactRestore));
        }).ToArray();
    }

    public static double SampleStdDev(IEnumerable<double> source)
    {
        double[] values = source.ToArray();
        if (values.Length < 2) return 0;
        double mean = values.Average();
        return Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / (values.Length - 1));
    }
}
