using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Metrics;
namespace TelemetryLink.Core.Optimization;

public sealed record ReliabilityCell(string Scenario, int ChannelSeed, int Repeat, double Q,
    double CriticalLoss, double Limit, bool ExactRestore, bool ReceiverCrashed,
    int CriticalFrames, int RecoveredCritical, long SentBits, double ElapsedMs);

public sealed record ReliabilityAssessment(int Cells, int IntegrityFailures, int ThresholdFailures,
    double MeanQ, double MeanExcess, double WorstExcess, bool Feasible, double Score);

public sealed record ReliabilityEvaluation(Tuning Tuning, ReliabilityAssessment Assessment,
    IReadOnlyList<ReliabilityCell> Cells);

/// <summary>Надійність має перевагу над Q; жоден сценарій/seed не пропускається через ранню відмову.</summary>
public static class ReliabilityFitness
{
    public static ReliabilityAssessment Assess(IReadOnlyList<ReliabilityCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0) throw new ArgumentException("Потрібна непорожня сітка оцінювання.");
        if (cells.Any(c => !double.IsFinite(c.Q) || c.Q < 0 || c.Q > 1 ||
            !double.IsFinite(c.CriticalLoss) || c.CriticalLoss < 0 || c.CriticalLoss > 1 ||
            !double.IsFinite(c.Limit) || c.Limit < 0 || c.Limit > 1))
            throw new ArgumentException("Q, loss і limit мають бути скінченними в межах 0..1.");
        int integrity = cells.Count(c => !c.ExactRestore || c.ReceiverCrashed);
        int failed = cells.Count(c => c.CriticalLoss > c.Limit + 1e-9);
        double meanQ = cells.Average(c => c.Q);
        double meanExcess = cells.Average(c => Math.Max(0, c.CriticalLoss - c.Limit));
        double worstExcess = cells.Max(c => Math.Max(0, c.CriticalLoss - c.Limit));
        bool feasible = integrity == 0 && failed == 0;
        // Disjoint ranges. For fixed N: fewer failed cells outranks any severity difference.
        double score = integrity > 0 ? -3.0 - (double)integrity / cells.Count
            : feasible ? 1.0 + meanQ
            : -(double)failed / cells.Count - meanExcess / (cells.Count + 1);
        return new(cells.Count, integrity, failed, meanQ, meanExcess, worstExcess, feasible, score);
    }

    public static ReliabilityEvaluation Evaluate(Tuning tuning, IReadOnlyList<ScenarioConfig> scenarios,
        IReadOnlyList<int> seeds, int repeats, Func<string, double> limit,
        Func<Tuning, ScenarioConfig, int, SessionReport> run)
    {
        ArgumentNullException.ThrowIfNull(run); ArgumentNullException.ThrowIfNull(limit);
        if (scenarios.Count == 0 || scenarios.Select(s => s.Name).Distinct().Count() != scenarios.Count ||
            seeds.Count == 0 || seeds.Distinct().Count() != seeds.Count || repeats < 1)
            throw new ArgumentException("Потрібні різні сценарії/seed та додатні повтори.");
        var cells = new List<ReliabilityCell>();
        foreach (var scenario in scenarios)
            foreach (int seed in seeds)
                for (int repeat = 1; repeat <= repeats; repeat++)
                {
                    var report = run(tuning, scenario, seed);
                    cells.Add(new(scenario.Name, seed, repeat, report.Q, report.CriticalLossRatio,
                        limit(scenario.Name), report.ExactRestore, report.ReceiverCrashed,
                        report.CriticalFrameCount, report.RecoveredCriticalFrameCount,
                        report.SentBits, report.ElapsedSeconds * 1000));
                }
        return new(tuning, Assess(cells), cells.AsReadOnly());
    }
}
