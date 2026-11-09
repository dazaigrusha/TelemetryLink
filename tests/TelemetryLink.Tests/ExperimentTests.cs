using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Tests;

public class ExperimentTests
{
    private static PipelineOptions Options(string packer = "sequential") => new()
    {
        Codec = AlgorithmCatalog.Codec("none"), Packer = AlgorithmCatalog.Packer(packer),
        Scheduler = AlgorithmCatalog.Scheduler("sequential"), Classifier = AlgorithmCatalog.Classifier("majority")
    };
    private static readonly SessionReport Template = new SessionRunner(DataKit.Config, Options()).Run(
        new TelemetrySession(DataKit.Session.Frames.Take(1).ToArray(), null), DataKit.Config.Scenario("calm"), 1, new PerfectChannel());

    [Fact]
    public void WarmupIsExcluded_SeedsArePaired_AndStatisticsMatchKnownNumbers()
    {
        ExperimentVariant[] variants = [new("base", Options()), new("other", Options("dp"))];
        var calls = new Dictionary<(string, int), int>();
        var runs = ComparisonExperiment.Run(variants, [DataKit.Config.Scenario("calm")], [7, 8], 3, 2,
            (variant, scenario, seed) =>
            {
                var key = (variant.Name, seed);
                int call = calls[key] = calls.GetValueOrDefault(key) + 1;
                double q = call <= 2 ? 0.99 : (call - 2) * 0.1 + (variant.Name == "other" ? 0.2 : 0);
                return Template with { Q = q, ScenarioName = scenario.Name };
            });
        Assert.Equal(20, calls.Values.Sum());
        Assert.Equal(12, runs.Count);
        Assert.All(runs, r => Assert.InRange(r.Repeat, 1, 3));
        var summary = ComparisonExperiment.Summarize(runs, "base");
        Assert.Equal(0.2, summary[0].MeanQ, 12);
        Assert.Equal(0.4, summary[1].MeanQ, 12);
        Assert.Equal(0.2, summary[1].MeanDeltaQ, 12);
        Assert.Equal(Math.Sqrt(0.008), summary[0].StdDevQ, 12);
        Assert.Equal(6, summary[0].Samples);
    }

    [Fact]
    public void EvaluationOrderRotates_ButOutputOrderRemainsStable()
    {
        var order = new List<string>();
        var runs = ComparisonExperiment.Run([new("base", Options()), new("other", Options("dp"))],
            [DataKit.Config.Scenario("calm")], [1], 3, 0,
            (variant, _, _) => { order.Add(variant.Name); return Template; });
        Assert.Equal(new[] { "base", "other", "other", "base", "base", "other" }, order);
        Assert.Equal(new[] { "base", "other", "base", "other", "base", "other" }, runs.Select(r => r.Variant));
    }

    [Fact]
    public void UnchangedAblation_ReusesBaselineWithoutInventingNewMeasurements()
    {
        var options = Options(); int calls = 0;
        var runs = ComparisonExperiment.Run([new("base", options), new("unchanged", options, true)],
            [DataKit.Config.Scenario("calm")], [1], 2, 1,
            (_, _, _) => { calls++; return Template; });
        Assert.Equal(3, calls);
        Assert.Equal(4, runs.Count);
        var alias = ComparisonExperiment.Summarize(runs, "base").Single(s => s.Variant == "unchanged");
        Assert.Equal(0, alias.MeasuredRuns);
        Assert.Equal(0.0, alias.MeanDeltaQ);
        Assert.All(runs.Where(r => r.Variant == "unchanged"), r => Assert.True(r.ReusedBaseline));
    }

    [Fact]
    public void ChangedConfiguration_CannotBeMarkedAsUnchanged()
        => Assert.Throws<ArgumentException>(() => ComparisonExperiment.Run(
            [new("base", Options()), new("alias", Options("dp"), true)], [DataKit.Config.Scenario("calm")], [1], 1, 0,
            (_, _, _) => Template));

    [Fact]
    public void FailedMeasuredRuns_AreRetainedRatherThanDropped()
    {
        var runs = ComparisonExperiment.Run([new("base", Options())], [DataKit.Config.Scenario("calm")], [1], 1, 0,
            (_, _, _) => Template with { Q = 0, ExactRestore = false });
        Assert.Single(runs);
        var summary = Assert.Single(ComparisonExperiment.Summarize(runs, "base"));
        Assert.False(summary.ExactRestore);
        Assert.Equal(0.0, summary.MeanQ);
    }

    [Fact]
    public void FailedWarmup_StopsExperiment()
        => Assert.Throws<InvalidDataException>(() => ComparisonExperiment.Run([new("base", Options())],
            [DataKit.Config.Scenario("calm")], [1], 1, 1, (_, _, _) => Template with { ExactRestore = false }));

    [Fact]
    public void SampleStandardDeviation_UsesNMinusOne()
    {
        Assert.Equal(1.0, ComparisonExperiment.SampleStdDev([1, 2, 3]));
        Assert.Equal(0.0, ComparisonExperiment.SampleStdDev([0.5]));
    }

    [Fact]
    public void DuplicateSeeds_AreRejected()
        => Assert.Throws<ArgumentException>(() => ComparisonExperiment.Run([new("base", Options())],
            [DataKit.Config.Scenario("calm")], [1, 1], 1, 0, (_, _, _) => Template));
}
